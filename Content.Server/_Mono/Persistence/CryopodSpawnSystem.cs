using System.Linq;
using Content.Server._FarHorizons.StarSystem;
using Content.Server._NF.CryoSleep;
using Content.Server.Administration.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Station.Systems;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Content.Shared._Mono.Persistence;
using Content.Shared.Bed.Sleep;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Persistence;

public sealed partial class CryopodSpawnSystem : EntitySystem
{
    [Dependency] private AdminSystem _admin = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;
    [Dependency] private UserDbDataManager _userDb = default!;
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private CryopodRespawnSystem _cryoRespawns = default!;
    [Dependency] private CryoSleepSystem _cryoSleep = default!;
    [Dependency] private SharedContainerSystem _cryoContainers = default!;
    [Dependency] private EuiManager _cryoSpawnEui = default!;

    private readonly Dictionary<NetUserId, (int Slot, Guid? Pod, HashSet<Guid> Forfeits)> _cryoReady = new();
    private readonly Dictionary<NetUserId, CryopodSpawnEui> _readyPrompts = new();

    public override void Initialize()
    {
        SubscribeNetworkEvent<CryopodSpawnOptionsRequest>(OnCryopodOptions);
        SubscribeNetworkEvent<CryopodSpawnRequest>(OnCryopodRequest);
        SubscribeLocalEvent<PlayerReadyRequestEvent>(OnReadyRequest);
        SubscribeLocalEvent<RoundStartSpawnSelectionEvent>(OnRoundStartSpawns);
        SubscribeLocalEvent<PlayerLateJoinRequestEvent>(OnLateJoinRequest);
        SubscribeLocalEvent<StarSystemPlanetsLoadedEvent>(OnPlanetsLoaded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _cryoReady.Clear();
            CloseReadyPrompts();
        });
    }

    private bool CanChooseCryopod(ICommonSession player) =>
        !_ticker.DummyTicker && player.Channel.IsConnected && _userDb.IsLoadComplete(player) &&
        _ticker.PlayerGameStatuses.TryGetValue(player.UserId, out var status) && status != PlayerGameStatus.JoinedGame;

    private CryopodSpawnOption[] GetCryopodOptions(ICommonSession player, List<CryopodRespawnSystem.ReservationChoice> choices)
    {
        if (_cryoSleep.HasCryosleepingBody(player.UserId))
            return [];

        return choices.Where(choice => choice.Available).Select(choice => ToOption(choice, true)).ToArray();
    }

    private CryopodSpawnOption ToOption(CryopodRespawnSystem.ReservationChoice choice, bool canWake)
    {
        var job = _prototypes.TryIndex<JobPrototype>(choice.Reservation.Job ?? string.Empty, out var prototype)
            ? prototype.LocalizedName
            : "???";
        return new CryopodSpawnOption(choice.Reservation.Id, choice.Planet.Name, job, choice.Reservation.Slot,
            choice.Planet.Persistence == PlanetPersistenceMode.Full && !string.IsNullOrEmpty(choice.Reservation.Body), canWake);
    }

    private void OnCryopodOptions(CryopodSpawnOptionsRequest request, EntitySessionEventArgs args) =>
        SendOptions(args.SenderSession);

    private void OnPlanetsLoaded(StarSystemPlanetsLoadedEvent ev)
    {
        foreach (var player in _players.Sessions)
            SendOptions(player);
    }

    private void SendOptions(ICommonSession player)
    {
        if (!player.Channel.IsConnected || !_userDb.IsLoadComplete(player))
            return;

        var selectedSlot = _cryoRespawns.SelectedSlot(player);
        var canWake = CanChooseCryopod(player) && _ticker.RunLevel == GameRunLevel.InRound &&
            !_ticker.DisallowLateJoin && !_cryoSleep.HasCryosleepingBody(player.UserId);
        var options = _cryoRespawns.FindReservations(player.UserId)
            .Select(choice => ToOption(choice, canWake && choice.Available && choice.Reservation.Slot == selectedSlot))
            .ToArray();
        RaiseNetworkEvent(new CryopodSpawnOptions(options), player);
    }

    private void OnCryopodRequest(CryopodSpawnRequest request, EntitySessionEventArgs args)
    {
        var player = args.SenderSession;
        if (CanChooseCryopod(player) && _ticker.RunLevel == GameRunLevel.InRound && !_ticker.DisallowLateJoin &&
            !TryWakeFromReservation(player, _cryoRespawns.SelectedSlot(player), request.Id))
            CryopodUnavailable(player);
    }

    private bool TryWakeFromReservation(ICommonSession player, int slot, Guid id)
    {
        if (_cryoSleep.HasCryosleepingBody(player.UserId))
            return false;

        var choice = _cryoRespawns.FindReservations(player.UserId, slot)
            .Find(entry => entry.Available && entry.Reservation.Id == id);
        if (choice == null || !TryComp<CryoSleepComponent>(choice.Pod, out var pod))
            return false;

        _prototypes.TryIndex<JobPrototype>(choice.Reservation.Job ?? string.Empty, out var job);
        var full = choice.Planet.Persistence == PlanetPersistenceMode.Full;
        var restoredBody = full && !string.IsNullOrEmpty(choice.Reservation.Body) ? _cryoRespawns.LoadBody(choice) : null;
        var profile = restoredBody == null ? _cryoRespawns.GetProfile(player.UserId, slot) : null;
        if (restoredBody == null && profile == null)
            return false;

        var body = restoredBody ?? _stationSpawning.SpawnPlayerMob(
            Transform(choice.Pod).Coordinates, job?.ID, profile, null, session: player);
        if (!_cryoContainers.Insert(body, pod.BodyContainer))
        {
            Del(body);
            return false;
        }

        if (restoredBody == null && full)
            _chat.DispatchServerMessage(player, Loc.GetString("cryopod-respawn-gear-reset"));

        var mind = _mind.CreateMind(player.UserId, MetaData(body).EntityName);
        _mind.SetUserId(mind, player.UserId);
        _mind.TransferTo(mind, body);
        if (job != null)
            _roles.MindAddJobRole(mind, silent: true, jobPrototype: job.ID);

        EnsureComp<SleepingComponent>(body).CooldownEnd = TimeSpan.FromSeconds(5);
        _cryoRespawns.TrackBody(body, player.UserId, slot);
        _admin.UpdatePlayerList(player);
        _ticker.PlayerJoinGame(player, silent: true);
        return true;
    }

    private void CryopodUnavailable(ICommonSession player) =>
        _chat.DispatchServerMessage(player, Loc.GetString("cryopod-respawn-unavailable"));

    private void OnReadyRequest(PlayerReadyRequestEvent request)
    {
        var player = request.Player;
        _cryoReady.Remove(player.UserId);
        if (_readyPrompts.Remove(player.UserId, out var previous))
            CloseReadyPrompt(previous);
        if (!request.Ready || request.Deferred)
            return;

        var slot = _cryoRespawns.SelectedSlot(player);
        var choices = _cryoRespawns.FindReservations(player.UserId, slot);
        if (choices.Count == 0)
            return;

        request.Deferred = true;
        var options = GetCryopodOptions(player, choices);
        var forfeits = choices.Select(choice => choice.Reservation.Id).ToHashSet();
        var prompt = new CryopodSpawnEui(CreateCryopodState(options, choices), id =>
        {
            if (!_readyPrompts.Remove(player.UserId) || _ticker.RunLevel != GameRunLevel.PreRoundLobby || !CanChooseCryopod(player) ||
                _cryoRespawns.SelectedSlot(player) != slot || (id != null && !options.Any(option => option.Id == id)))
                return;

            _cryoReady[player.UserId] = (slot, id, forfeits);
            request.Complete();
        });
        _readyPrompts.Add(player.UserId, prompt);
        _cryoSpawnEui.OpenEui(prompt, player);
    }

    private void OnRoundStartSpawns(RoundStartSpawnSelectionEvent ev)
    {
        CloseReadyPrompts();
        var players = ev.Players;
        for (var i = players.Count - 1; i >= 0; i--)
        {
            var player = players[i];
            var slot = _cryoRespawns.SelectedSlot(player);
            if (!_cryoReady.Remove(player.UserId, out var choice))
                continue;
            if (choice.Slot == slot && choice.Pod is { } pod && TryWakeFromReservation(player, slot, pod))
            {
                players.RemoveAt(i);
                ev.Profiles.Remove(player.UserId);
                continue;
            }

            if (_cryoRespawns.FindReservations(player.UserId, slot)
                .Any(entry => choice.Slot != slot || choice.Pod != null || !choice.Forfeits.Contains(entry.Reservation.Id)))
            {
                _ticker.SetReadyStatus(player, false);
                CryopodUnavailable(player);
                players.RemoveAt(i);
                ev.Profiles.Remove(player.UserId);
            }
        }
    }

    private void CloseReadyPrompts()
    {
        foreach (var prompt in _readyPrompts.Values)
            CloseReadyPrompt(prompt);
        _readyPrompts.Clear();
    }

    private static void CloseReadyPrompt(CryopodSpawnEui prompt)
    {
        if (!prompt.IsShutDown && prompt.Player.Channel.IsConnected)
            prompt.Close();
    }

    private void OnLateJoinRequest(PlayerLateJoinRequestEvent request)
    {
        if (!request.Deferred && CanChooseCryopod(request.Player) && !_ticker.DisallowLateJoin)
            request.Deferred = ConfirmNormalSpawn(request);
    }

    private bool ConfirmNormalSpawn(PlayerLateJoinRequestEvent request)
    {
        var player = request.Player;
        var roundId = _ticker.RoundId;
        var slot = _cryoRespawns.SelectedSlot(player);
        var choices = _cryoRespawns.FindReservations(player.UserId, slot);
        if (choices.Count == 0)
            return false;

        var forfeits = choices.Select(choice => choice.Reservation.Id).ToHashSet();
        _cryoSpawnEui.OpenEui(new CryopodSpawnEui(CreateCryopodState([], choices), id =>
        {
            if (id != null || _ticker.RoundId != roundId || _ticker.RunLevel != GameRunLevel.InRound ||
                _ticker.DisallowLateJoin || !CanChooseCryopod(player) ||
                _cryoRespawns.SelectedSlot(player) != slot)
                return;

            if (_cryoRespawns.FindReservations(player.UserId, slot).Any(entry => !forfeits.Contains(entry.Reservation.Id)))
                ConfirmNormalSpawn(request);
            else if (request.Job != null && (!_stationJobs.TryGetJobSlot(request.Station, request.Job, out var slots) || slots == 0))
                _chat.DispatchServerMessage(player, Loc.GetString("game-ticker-player-no-jobs-available-when-joining"));
            else
                request.Complete();
        }), player);
        return true;
    }

    private static CryopodSpawnEuiState CreateCryopodState(
        CryopodSpawnOption[] options, List<CryopodRespawnSystem.ReservationChoice> choices)
        => new(options, string.Join(", ", choices.Select(choice => choice.Planet.Name).Distinct()));
}
