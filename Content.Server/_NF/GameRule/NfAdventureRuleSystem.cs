using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Server._NF.Bank;
using Content.Server._NF.GameRule.Components;
using Content.Server._NF.GameTicking.Events;
using Content.Server.Cargo.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.GameTicking.Rules;
using Content.Shared._NF.Bank;
using Content.Shared._NF.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Server;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._NF.GameRule;

/// <summary>
/// This handles the dungeon and trading post spawning, as well as round end capitalism summary
/// </summary>
public sealed class NFAdventureRuleSystem : GameRuleSystem<NFAdventureRuleComponent>
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly PointOfInterestSystem _poi = default!;
    [Dependency] private readonly IBaseServer _baseServer = default!;
    [Dependency] private readonly IEntitySystemManager _entSys = default!;

    private readonly HttpClient _httpClient = new();

    private readonly ProtoId<GamePresetPrototype> _fallbackPresetID = "NFPirates";

    private const int LeaderboardLimit = 20;

    private readonly LocId _summaryProfitLocId = "adventure-list-profit";
    private readonly LocId _summaryLossLocId = "adventure-list-loss";
    private readonly LocId _summaryAdventureHighLocId = "adventure-list-high";
    private readonly LocId _summaryAdventureLowLocId = "adventure-list-low";
    private readonly LocId _summaryAdventureNoEntriesLocId = "adventure-webhook-list-no-entries";

    private readonly string _summaryProfitColor = "[color=#d19e5e]";
    private readonly string _summaryLossColor = "[color=#659cc9]";

    public sealed class PlayerRoundBankInformation
    {
        // Initial balance, obtained on spawn
        public int StartBalance;
        // Ending balance, obtained on game end or detach (NOTE: multiple detaches possible), whichever happens first.
        public int EndBalance;
        // Entity name: used for display purposes ("The Feel of Fresh Bills earned 100,000 spesos")
        public string Name;
        // User ID: used to validate incoming information.
        // If, for whatever reason, another player takes over this character, their initial balance is inaccurate.
        public NetUserId UserId;

        public PlayerRoundBankInformation(int startBalance, string name, NetUserId userId)
        {
            StartBalance = startBalance;
            EndBalance = -1;
            Name = name;
            UserId = userId;
        }
    }

    // A list of player bank account information stored by the controlled character's entity.
    [ViewVariables]
    private Dictionary<EntityUid, PlayerRoundBankInformation> _players = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawningEvent);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetachedEvent);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        _player.PlayerStatusChanged += PlayerManagerOnPlayerStatusChanged;
    }

    protected override void AppendRoundEndText(EntityUid uid, NFAdventureRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent ev)
    {
        ev.AddLine(Loc.GetString("adventure-list-start"));
        var allScores = new List<BankData>();

        GetBuiltRoundEndSummary(ref ev, ref allScores);

        if (allScores.Count == 0)
            return;

        HandleHighestLowestEarners(allScores);
        ReportLedger();
    }

    private void GetBuiltRoundEndSummary(ref RoundEndTextAppendEvent ev, ref List<BankData> bankData)
    {
        foreach (var (player, playerInfo) in _players)
        {
            var endBalance = playerInfo.EndBalance;

            if (_bank.TryGetBalance(player, out var bankBalance))
            {
                endBalance = bankBalance;
            }

            // Check if endBalance is valid (non-negative)
            if (endBalance < 0)
                continue;

            var profit = endBalance - playerInfo.StartBalance;
            var playerBankData = new BankData()
            {
                PlayerName = playerInfo.Name,
                Profit = profit
            };

            bankData.Add(playerBankData);
        }

        var orderedData = bankData.OrderByDescending(data => data.Profit);

        // Sort by profit
        foreach (var data in orderedData)
        {
            var profit = Math.Abs(data.Profit);
            var profitInSpesos = BankSystemExtensions.ToSpesoString(profit);
            var localeId = data.Profit >= 0 ? _summaryProfitLocId : _summaryLossLocId;
            var color = data.Profit >= 0 ? _summaryProfitColor : _summaryLossColor;

            var amountText = $"{color}{profitInSpesos}[/color]";
            var summaryText = Loc.GetString(localeId, ("amount", amountText));

            ev.AddLine($"- {data.PlayerName} {summaryText}");
        }
    }

    private string GetTopFor(List<BankData> orderedData, LocId adventureWebhookId, bool reverse = false)
    {
        if (reverse)
            orderedData.Reverse();

        var noEntries = Loc.GetString(_summaryAdventureNoEntriesLocId);
        var builder = new StringBuilder();
        var takeAmount = LeaderboardLimit / 2;

        if (orderedData.Count == 0)
        {
            builder.Append(noEntries);
            return builder.ToString();
        }

        for (var i = 0; i < takeAmount; i++)
        {
            if (orderedData.Count == 0)
                break;

            var first = orderedData.First();

            // If we're reversing it, assume we want only losses. Otherwise, only profit.
            if (reverse && first.Profit > 0 || !reverse && first.Profit < 0)
                break;

            var profit = Math.Abs(first.Profit);
            var profitInSpesos = BankSystemExtensions.ToSpesoString(profit);
            var profitText = Loc.GetString(adventureWebhookId, ("amount", profitInSpesos));
            var realText = $"{first.PlayerName} {profitText}";

            builder.AppendLine(realText);
            orderedData.RemoveAt(0);
        }

        var finalString = builder.ToString();
        return string.IsNullOrWhiteSpace(finalString) ? noEntries : finalString;
    }

    private void HandleHighestLowestEarners(List<BankData> bankData)
    {
        var builder = new StringBuilder();
        var orderedData = bankData.OrderByDescending(data => data.Profit).ToList();

        var highestProfits = GetTopFor(orderedData, _summaryProfitLocId);
        var highestLosses = GetTopFor(orderedData, _summaryLossLocId, reverse: true);

        var highText = Loc.GetString("adventure-webhook-list-high");
        var lowText = Loc.GetString("adventure-webhook-list-low");

        builder.AppendLine(highText);
        builder.AppendLine(highestProfits);

        builder.AppendLine(string.Empty);

        builder.AppendLine(lowText);
        builder.AppendLine(highestLosses);

        var finalRelayText = FormattedMessage.RemoveMarkupPermissive(builder.ToString());
        ReportRound(finalRelayText);
    }

    private void OnPlayerSpawningEvent(PlayerSpawnCompleteEvent ev)
    {
        if (ev.Player.AttachedEntity is { Valid: true } mobUid)
        {
            EnsureComp<CargoSellBlacklistComponent>(mobUid);

            // Store player info with the bank balance - we have it directly, and BankSystem won't have a cache yet.
            if (!_players.ContainsKey(mobUid))
                _players[mobUid] = new PlayerRoundBankInformation(ev.Profile.BankBalance, MetaData(mobUid).EntityName, ev.Player.UserId);
        }
    }

    private void OnPlayerDetachedEvent(PlayerDetachedEvent ev)
    {
        if (ev.Entity is not { Valid: true } mobUid)
            return;

        // if player doesn't have bank information, cut early
        if (!_players.TryGetValue(mobUid, out var value))
            return;

        // get the players balance
        if (value.UserId == ev.Player.UserId &&
            _bank.TryGetBalance(ev.Player, out var bankBalance))
        {
            value.EndBalance = bankBalance;
        }
    }

    private void PlayerManagerOnPlayerStatusChanged(object? _, SessionStatusEventArgs e)
    {
        // Treat all disconnections as being possibly final.
        if (e.NewStatus != SessionStatus.Disconnected ||
            e.Session.AttachedEntity == null)
            return;

        var mobUid = e.Session.AttachedEntity.Value;

        // checking if player has any bank information, if not, leave early
        if (!_players.TryGetValue(mobUid, out var value))
            return;

        // get the players balance
        if (value.UserId == e.Session.UserId &&
            _bank.TryGetBalance(e.Session, out var bankBalance))
        {
            value.EndBalance = bankBalance;
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _players.Clear();
    }

    /// <summary>
    /// Mono: Gets profit information for a player by their character name.
    /// </summary>
    public BankData? GetBankDataInfo(NetUserId userId, string characterName)
    {
        // Find the player by character name and user ID
        var playerEntry = _players.FirstOrDefault(kvp =>
            kvp.Value.UserId == userId && kvp.Value.Name == characterName);

        if (playerEntry.Key == EntityUid.Invalid)
            return null;

        var playerInfo = playerEntry.Value;
        var endBalance = playerInfo.EndBalance;

        // Try to get current balance if end balance wasn't set
        if (_bank.TryGetBalance(playerEntry.Key, out var bankBalance))
        {
            endBalance = bankBalance;
        }

        // Check if endBalance is valid (non-negative)
        if (endBalance < 0)
            return null;

        var profit = endBalance - playerInfo.StartBalance;
        return new BankData(characterName, profit);
    }

    public string ConvertBankDataToString(BankData bankData, bool removeColor = false)
    {
        var absoluteProfit = Math.Abs(bankData.Profit);
        var adventureWebhookId = bankData.Profit > 0 ? _summaryProfitLocId : _summaryLossLocId;
        var profitInSpesos = BankSystemExtensions.ToSpesoString(absoluteProfit);
        var profitText = Loc.GetString(adventureWebhookId, ("amount", profitInSpesos));

        if (removeColor)
            profitText = FormattedMessage.RemoveMarkupPermissive(profitText);

        return $"{bankData.PlayerName} {profitText}";
    }

    protected override void Started(EntityUid uid, NFAdventureRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        var mapUid = GameTicker.DefaultMap;

        //First, we need to grab the list and sort it into its respective spawning logics
        List<PointOfInterestPrototype> depotProtos = [];
        List<PointOfInterestPrototype> marketProtos = [];
        List<PointOfInterestPrototype> requiredProtos = [];
        List<PointOfInterestPrototype> optionalProtos = [];
        Dictionary<string, List<PointOfInterestPrototype>> remainingUniqueProtosBySpawnGroup = new();

        var currentPreset = _ticker.CurrentPreset?.ID ?? _fallbackPresetID;

        foreach (var location in _proto.EnumeratePrototypes<PointOfInterestPrototype>())
        {
            // Check if any preset is accepted (empty) or if current preset is supported.
            var protoIds = location.SpawnGamePreset.ToList();
            if (location.SpawnGamePreset.Length > 0 && !protoIds.Contains(currentPreset))
                continue;

            switch (location.SpawnGroup)
            {
                case "CargoDepot":
                    depotProtos.Add(location);
                    break;
                case "MarketStation":
                    marketProtos.Add(location);
                    break;
                case "Required":
                    requiredProtos.Add(location);
                    break;
                case "Optional":
                    optionalProtos.Add(location);
                    break;
                // the remainder are done on a per-poi-per-group basis
                default:
                {
                    if (!remainingUniqueProtosBySpawnGroup.ContainsKey(location.SpawnGroup))
                        remainingUniqueProtosBySpawnGroup[location.SpawnGroup] = new();
                    remainingUniqueProtosBySpawnGroup[location.SpawnGroup].Add(location);
                    break;
                }
            }
        }
        _poi.GenerateDepots(mapUid, depotProtos, out component.CargoDepots);
        _poi.GenerateMarkets(mapUid, marketProtos, out component.MarketStations);
        _poi.GenerateRequireds(mapUid, requiredProtos, out component.RequiredPois);
        _poi.GenerateOptionals(mapUid, optionalProtos, out component.OptionalPois);
        _poi.GenerateUniques(mapUid, remainingUniqueProtosBySpawnGroup, out component.UniquePois);

        base.Started(uid, component, gameRule, args);

        // Using invalid entity, we don't have a relevant entity to reference here.
        RaiseLocalEvent(EntityUid.Invalid, new StationsGeneratedEvent(), broadcast: true); // TODO: attach this to a meaningful entity.
    }

    private async Task ReportRound(string message, int color = 0x77DDE7)
    {
        Logger.InfoS("discord", message);
        var webhookUrl = _cfg.GetCVar(NFCCVars.DiscordLeaderboardWebhook);

        if (webhookUrl == string.Empty)
            return;

        var serverName = _baseServer.ServerName;
        var gameTicker = _entSys.GetEntitySystemOrNull<GameTicker>();
        var runId = gameTicker?.RoundId ?? 0;

        var payload = new WebhookPayload
        {
            Embeds =
            [
                new Embed
                {
                    Title = Loc.GetString("adventure-webhook-list-start"),
                    Description = message,
                    Color = color,
                    Footer = new EmbedFooter
                    {
                        Text = Loc.GetString(
                            "adventure-webhook-footer",
                            ("serverName", serverName),
                            ("roundId", runId)),
                    },
                },

            ],
        };
        await SendWebhookPayload(webhookUrl, payload);
    }

    private async Task ReportLedger(int color = 0xBF863F)
    {
        var webhookUrl = _cfg.GetCVar(NFCCVars.DiscordLeaderboardWebhook);
        if (webhookUrl == string.Empty)
            return;

        var ledgerPrintout = _bank.GetLedgerPrintout();
        if (string.IsNullOrEmpty(ledgerPrintout))
            return;
        Logger.InfoS("discord", ledgerPrintout);

        var serverName = _baseServer.ServerName;
        var gameTicker = _entSys.GetEntitySystemOrNull<GameTicker>();
        var runId = gameTicker?.RoundId ?? 0;

        var payload = new WebhookPayload
        {
            Embeds =
            [
                new Embed
                {
                    Title = Loc.GetString("adventure-webhook-ledger-start"),
                    Description = ledgerPrintout,
                    Color = color,
                    Footer = new EmbedFooter
                    {
                        Text = Loc.GetString(
                            "adventure-webhook-footer",
                            ("serverName", serverName),
                            ("roundId", runId)),
                    },
                },

            ],
        };

        await SendWebhookPayload(webhookUrl, payload);
    }

    private async Task SendWebhookPayload(string webhookUrl, WebhookPayload payload)
    {
        var serializedPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(serializedPayload, Encoding.UTF8, "application/json");
        var request = await _httpClient.PostAsync($"{webhookUrl}?wait=true", content);
        var reply = await request.Content.ReadAsStringAsync();
        if (!request.IsSuccessStatusCode)
        {
            Logger.ErrorS("mining", $"Discord returned bad status code when posting message: {request.StatusCode}\nResponse: {reply}");
        }
    }

    // https://discord.com/developers/docs/resources/channel#message-object-message-structure
    private struct WebhookPayload
    {
        [JsonPropertyName("username")] public string? Username { get; set; } = null;

        [JsonPropertyName("avatar_url")] public string? AvatarUrl { get; set; } = null;

        [JsonPropertyName("content")] public string Message { get; set; } = "";

        [JsonPropertyName("embeds")] public List<Embed>? Embeds { get; set; } = null;

        [JsonPropertyName("allowed_mentions")]
        public Dictionary<string, string[]> AllowedMentions { get; set; } =
            new()
            {
                { "parse", Array.Empty<string>() },
            };

        public WebhookPayload()
        {
        }
    }

    // https://discord.com/developers/docs/resources/channel#embed-object-embed-structure
    private struct Embed
    {
        [JsonPropertyName("title")] public string Title { get; set; } = "";

        [JsonPropertyName("description")] public string Description { get; set; } = "";

        [JsonPropertyName("color")] public int Color { get; set; } = 0;

        [JsonPropertyName("footer")] public EmbedFooter? Footer { get; set; } = null;

        public Embed()
        {
        }
    }

    // https://discord.com/developers/docs/resources/channel#embed-object-embed-footer-structure
    private struct EmbedFooter
    {
        [JsonPropertyName("text")] public string Text { get; set; } = "";

        [JsonPropertyName("icon_url")] public string? IconUrl { get; set; }

        public EmbedFooter()
        {
        }
    }
}

public record struct BankData
{
    public string PlayerName { get; set; }
    public int Profit { get; set; }

    public BankData(string playerName, int profit)
    {
        PlayerName = playerName;
        Profit = profit;
    }
}
