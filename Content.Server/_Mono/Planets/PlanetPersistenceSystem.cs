using System.Globalization;
using System.IO;
using System.Linq;
using Content.Server._FarHorizons.StarSystem;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Content.Shared._Mono.CCVar;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Log;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Server._Mono.Planets;

public sealed partial class PlanetPersistenceSystem : EntitySystem
{
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ResPath Root = new("/persistence");
    private static readonly SoundSpecifier DefconSound = new SoundPathSpecifier("/Audio/_Mono/Effects/Alerts/psg_breach.ogg");

    private readonly Dictionary<string, (int Seed, ResPath? Folder)> _saves = new();
    private readonly Dictionary<string, TimeSpan> _lastSaves = new();
    private readonly Dictionary<string, TimeSpan> _nextSaveOverrides = new();
    private readonly HashSet<string> _finalSaves = new();
    private readonly Dictionary<string, TimeSpan> _pausedSaves = new();
    private ISawmill _sawmill = default!;
    private DateTime? _roundStartedAt;
    private bool _persistPlanets;
    private TimeSpan _saveInterval;
    private TimeSpan _roundStartTime;
    private bool _roundEnded;

    public override void Initialize()
    {
        _sawmill = _logManager.GetSawmill("persistence::planet");
        Subs.CVar(_cfg, MonoCVars.PersistPlanets, enabled =>
        {
            _persistPlanets = enabled;
            PanelChanged?.Invoke();
        }, true);
        Subs.CVar(_cfg, MonoCVars.PlanetPersistInterval, minutes =>
        {
            _saveInterval = TimeSpan.FromMinutes(minutes);
            PanelChanged?.Invoke();
        }, true);
        SubscribeLocalEvent<StarSystemPlanetsLoadedEvent>(_ => PanelChanged?.Invoke());
        SubscribeLocalEvent<PlanetMapGenerationComponent, EntityTerminatingEvent>(OnSurfaceTerminating);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnded);
        SubscribeLocalEvent<RoundRestartingEvent>(_ => SaveFinalPlanets());
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
    }

    private static bool CanPersist(PlanetTypePrototype planet) =>
        planet.Surface != null && planet.Persistence != PlanetPersistenceMode.None;

    public IReadOnlyList<PlanetTypePrototype> GetLoadedPlanets()
    {
        var loaded = new Dictionary<string, PlanetTypePrototype>();
        var planets = AllEntityQuery<PlanetBodyComponent>();
        while (planets.MoveNext(out var uid, out var body))
        {
            if (!TerminatingOrDeleted(uid) && _prototypes.TryIndex(body.Type, out var planet))
                loaded.TryAdd(planet.ID, planet);
        }

        return loaded.Values.ToArray();
    }

    public int? GetSeed(PlanetTypePrototype planet)
    {
        if (!CanPersist(planet))
            return null;

        if (!_saves.TryGetValue(planet.ID, out var save))
            _saves.Add(planet.ID, save = LoadSave(planet.ID) ?? (_random.Next(), null));

        return save.Seed;
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        _roundStartedAt = DateTime.UtcNow;
        _roundStartTime = _timing.CurTime;
        PanelChanged?.Invoke();
    }

    private void OnRoundEnded(RoundEndedEvent ev)
    {
        if (ev.RoundId != _ticker.RoundId)
            return;

        _roundEnded = true;
        SaveFinalPlanets();
        PanelChanged?.Invoke();
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        _roundStartedAt = null;
        _roundEnded = false;
        _saves.Clear();
        _lastSaves.Clear();
        _nextSaveOverrides.Clear();
        _finalSaves.Clear();
        PanelChanged?.Invoke();
    }

    public override void Update(float frameTime)
    {
        if (_persistPlanets && !_roundEnded && _roundStartedAt is { } startedAt &&
            (_nextSaveOverrides.Count > 0 || _saveInterval > TimeSpan.Zero &&
                _timing.CurTime - _roundStartTime >= _saveInterval))
            SavePlanets(startedAt, _ticker.RoundId, final: false);
    }

    private void SaveFinalPlanets()
    {
        if (_persistPlanets && _roundStartedAt is { } startedAt)
            SavePlanets(startedAt, _ticker.RoundId, final: true);
    }

    private (int Seed, ResPath? Folder)? LoadSave(string planetId)
    {
        var data = _resources.UserData;
        if (!ResPath.IsValidFilename(planetId))
            return null;

        var planetFolder = Root / planetId;
        try
        {
            if (!data.IsDir(planetFolder))
                return null;

            foreach (var entry in data.DirectoryEntries(planetFolder).OrderByDescending(name => name, StringComparer.Ordinal))
            {
                if (!ResPath.IsValidFilename(entry))
                    continue;

                var path = planetFolder / entry / "metadata.yml";
                if (!data.Exists(path))
                    continue;

                try
                {
                    using var reader = new StreamReader(data.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read));
                    var yaml = new YamlStream();
                    yaml.Load(reader);
                    if (yaml.Documents.Count == 1 && yaml.Documents[0].RootNode is YamlMappingNode mapping &&
                        mapping.Children.TryGetValue(new YamlScalarNode("seed"), out var node) &&
                        node is YamlScalarNode scalar &&
                        int.TryParse(scalar.Value, CultureInfo.InvariantCulture, out var seed))
                    {
                        return (seed, path.Directory);
                    }

                    _sawmill.Warning($"Invalid planet seed metadata at {path}.");
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or YamlException)
                {
                    _sawmill.Warning($"Could not read planet metadata at {path}: {e.Message}");
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _sawmill.Warning($"Could not list planet saves in {planetFolder}: {e.Message}");
        }

        return null;
    }

    private bool SavePlanets(DateTime startedAt, int roundId, bool final, string? onlyPlanet = null, TimeSpan? overrideInterval = null)
    {
        var planets = AllEntityQuery<PlanetBodyComponent>();
        while (planets.MoveNext(out var body))
        {
            var id = body.Type.Id;
            if (_pausedSaves.ContainsKey(id) || onlyPlanet != null && id != onlyPlanet || final && _finalSaves.Contains(id))
                continue;

            if (!final && onlyPlanet == null &&
                (NextSaveAt(id) is not { } due || _timing.CurTime < due))
                continue;

            if (body.SurfaceNetwork is not { } network || TerminatingOrDeleted(network) ||
                !TryComp<PlanetMapGenerationComponent>(network, out var generation) ||
                !_prototypes.TryIndex(body.Type, out var planet) || !CanPersist(planet))
                continue;

            var saved = SavePlanet(planet, generation, startedAt, roundId, final);
            if (!final && (saved || onlyPlanet == null))
            {
                _lastSaves[id] = _timing.CurTime;
                if (overrideInterval is { } next)
                    _nextSaveOverrides[id] = next;
                else
                    _nextSaveOverrides.Remove(id);
            }

#if DEBUG
            if (saved)
                AnnounceSave(planet.Name, final ? TimeSpan.Zero : overrideInterval ?? _saveInterval);
#endif
            if (saved && final)
                _finalSaves.Add(id);
            PanelChanged?.Invoke();
            if (onlyPlanet != null)
                return saved;
        }

        return false;
    }

    public bool ForceSave(string planetId, int? nextSaveMinutes) =>
        _persistPlanets && !_roundEnded && _roundStartedAt is { } startedAt &&
        nextSaveMinutes is null or > 0 &&
        SavePlanets(startedAt, _ticker.RoundId, final: false, onlyPlanet: planetId,
            overrideInterval: nextSaveMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : null);

    public string DeleteRound(string planetId, int roundId, bool confirm)
    {
        if (!ResPath.IsValidFilename(planetId) || roundId < 0)
            return Loc.GetString("planet-save-delete-not-found");

        var data = _resources.UserData;
        var planetFolder = Root / planetId;
        try
        {
            if (!data.IsDir(planetFolder))
                return Loc.GetString("planet-save-delete-not-found");

            var matches = data.DirectoryEntries(planetFolder).Where(name =>
                TryGetRoundId(name, out var savedRound) && savedRound == roundId &&
                data.IsDir(planetFolder / name)).ToArray();
            if (matches.Length == 0)
                return Loc.GetString("planet-save-delete-not-found");
            if (matches.Length > 1)
                return Loc.GetString("planet-save-delete-ambiguous");

            return DeleteSave(planetId, matches[0], confirm) ??
                Loc.GetString("planet-save-delete-ok", ("folder", (planetFolder / matches[0]).ToString()));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _sawmill.Error($"Could not delete save for {planetId}, round {roundId}: {e}");
            return Loc.GetString("planet-save-delete-failed");
        }
    }

    private bool CanDeleteSave(string planetId, string folder) =>
        _roundStartedAt is not { } startedAt || folder != RoundFolder(startedAt, _ticker.RoundId) ||
        _finalSaves.Contains(planetId);

    private string? DeleteSave(string planetId, string name, bool confirm, ICommonSession? admin = null)
    {
        if (!CanDeleteSave(planetId, name))
            return Loc.GetString("planet-save-delete-current");

        var folder = Root / planetId / name;
        if (!confirm)
            return Loc.GetString("planet-save-delete-confirm", ("folder", folder.ToString()));

        _resources.UserData.Delete(folder);
        if (_saves.TryGetValue(planetId, out var cached) && cached.Folder == folder)
            _saves.Remove(planetId);
        if (admin != null && TryGetRoundId(name, out var round))
            _chat.SendAdminAnnouncement(Loc.GetString("persistence-panel-delete-admin",
                ("admin", admin.Name), ("planet", planetId), ("round", round)));
        PanelChanged?.Invoke();
        return null;
    }

    private static string RoundFolder(DateTime startedAt, int roundId) =>
        FormattableString.Invariant($"{startedAt:yyyyMMddTHHmmssfffffffZ}-{roundId}");

    private static bool TryGetRoundId(string folderName, out int roundId)
    {
        roundId = default;
        var separator = folderName.LastIndexOf('-');
        return separator > 0 &&
               DateTime.TryParseExact(folderName.AsSpan(0, separator), "yyyyMMddTHHmmssfffffff'Z'",
                   CultureInfo.InvariantCulture, DateTimeStyles.None, out _) &&
               int.TryParse(folderName.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out roundId);
    }

    private bool SavePlanet(PlanetTypePrototype planet, PlanetMapGenerationComponent generation, DateTime startedAt, int roundId, bool final)
    {
        if (!ResPath.IsValidFilename(planet.ID))
        {
            _sawmill.Warning($"Planet ID {planet.ID} is not a valid persistence directory name.");
            return false;
        }

        var data = _resources.UserData;
        var folder = Root / planet.ID / RoundFolder(startedAt, roundId);
        var path = folder / "metadata.yml";
        try
        {
            data.CreateDir(folder);
            if (planet.Persistence is PlanetPersistenceMode.Semi or PlanetPersistenceMode.Full)
            {
                data.CreateDir(folder / "map");
                SaveStack(folder, planet, generation);
            }

            var metadata = new YamlMappingNode
            {
                { "roundId", roundId.ToString(CultureInfo.InvariantCulture) },
                { "planetId", planet.ID },
                { "persistence", planet.Persistence.ToString() },
                { "seed", generation.Seed.ToString(CultureInfo.InvariantCulture) },
                { "savedAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) },
                { "final", final ? "true" : "false" },
            };
            WriteYaml(path, new YamlDocument(metadata));
        }
        catch (Exception e)
        {
            _sawmill.Error($"Could not save planet {planet.ID} at {folder}: {e}");
            AlertAdmins("planet-save-failed-admin", planet.ID);
            return false;
        }

        var maxSaves = planet.Persistence == PlanetPersistenceMode.Terrain
            ? 1
            : Math.Max(1, _cfg.GetCVar(MonoCVars.PlanetMaxSaves));
        PruneSaves(planet.ID, folder, generation.Seed, maxSaves);
        return true;
    }

    private void PruneSaves(string planetId, ResPath current, int seed, int maxSaves)
    {
        var data = _resources.UserData;
        var planetFolder = current.Directory;
        try
        {
            var expired = data.DirectoryEntries(planetFolder)
                .Where(name => TryGetRoundId(name, out _))
                .OrderByDescending(name => name, StringComparer.Ordinal)
                .Select(name => planetFolder / name)
                .Where(folder => folder != current && data.IsDir(folder) && data.Exists(folder / "metadata.yml"))
                .Skip(maxSaves - 1)
                .ToArray();

            foreach (var folder in expired)
            {
                try
                {
                    data.Delete(folder);
                    if (_saves.TryGetValue(planetId, out var cached) && cached.Folder == folder)
                        _saves[planetId] = (seed, current);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    _sawmill.Warning($"Could not prune planet save at {folder}: {e.Message}");
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _sawmill.Warning($"Could not list planet saves for retention in {planetFolder}: {e.Message}");
        }
    }

#if DEBUG
    private void AnnounceSave(string planetName, TimeSpan interval)
    {
        var next = interval <= TimeSpan.Zero
            ? Loc.GetString("planet-save-next-none")
            : interval.TotalDays >= 1
                ? Loc.GetString("planet-save-next-days", ("count", interval.Days))
                : interval.TotalHours >= 1
                    ? Loc.GetString("planet-save-next-hours", ("count", interval.Hours))
                    : Loc.GetString("planet-save-next-minutes", ("count", interval.Minutes));
        var message = Loc.GetString("planet-save-success-admin", ("planet", planetName), ("next", next));
        var wrapped = Loc.GetString("chat-manager-send-admin-announcement-wrap-message",
            ("adminChannelName", Loc.GetString("chat-manager-admin-channel-name")),
            ("message", FormattedMessage.EscapeText(message)));
        _chat.ChatMessageToMany(ChatChannel.Admin, message, wrapped, EntityUid.Invalid, false, false,
            _admins.ActiveAdmins.Select(admin => admin.Channel), Color.LimeGreen);
    }
#endif

    public void AlertAdmins(string locId, string planetId)
    {
        var message = Loc.GetString(locId, ("planet", planetId));
        var recipients = Filter.Empty();
        foreach (var admin in _admins.AllAdmins)
        {
            recipients.AddPlayer(admin);
            if (_admins.IsAdmin(admin))
                _chat.SendAdminAnnouncementMessage(admin, message);
            else
                _chat.DispatchServerMessage(admin, message, suppressLog: true);
        }

        _audio.PlayGlobal(DefconSound, recipients, false);
    }

    private void WriteYaml(ResPath path, YamlDocument document)
    {
        var data = _resources.UserData;
        var rootDir = data.RootDir;
        var target = rootDir == null ? path : path.WithName(path.Filename + ".tmp");
        using (var writer = new StreamWriter(data.Open(target, FileMode.Create, FileAccess.Write, FileShare.None)))
            new YamlStream(document).Save(new YamlMappingFix(new Emitter(writer)), assignAnchors: false);

        if (rootDir != null)
            File.Move(Path.Join(rootDir, target.ToRelativeSystemPath()), Path.Join(rootDir, path.ToRelativeSystemPath()), overwrite: true);
    }
}
