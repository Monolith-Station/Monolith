using System.Linq;
using Content.Server._Mono.Planets;
using Content.Server._NF.CryoSleep;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Content.Shared._Mono.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Persistence;

public sealed partial class CryopodRespawnSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IServerPreferencesManager _preferences = default!;

    private readonly Dictionary<EntityUid, int> _profiles = new();
    private readonly Dictionary<NetUserId, (HumanoidCharacterProfile Profile, int Slot)> _spawning = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<TransformComponent, CryosleepEnterEvent>(OnCryoEnter);
        SubscribeLocalEvent<TransformComponent, CryosleepWakeUpEvent>(OnCryoWakeUp);
        SubscribeLocalEvent<CryopodRespawnComponent, EntityTerminatingEvent>(OnPodTerminating);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _profiles.Clear();
            _spawning.Clear();
        });
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        if (TryGetSlot(ev.Player.UserId, ev.Profile, out var slot))
            _spawning[ev.Player.UserId] = (ev.Profile, slot);
    }

    private bool TryGetSlot(NetUserId user, HumanoidCharacterProfile profile, out int slot)
    {
        slot = -1;
        return _preferences.TryGetCachedPreferences(user, out var prefs) && prefs.TryIndexOfCharacter(profile, out slot);
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        var user = ev.Player.UserId;
        if (_spawning.Remove(user, out var spawn) && ReferenceEquals(spawn.Profile, ev.Profile))
            TrackBody(ev.Mob, user, spawn.Slot);
        else if (TryGetSlot(user, ev.Profile, out var slot))
            TrackBody(ev.Mob, user, slot);
    }

    public bool CanSave(EntityUid body, EntityUid pod) =>
        _profiles.ContainsKey(body) && _cfg.GetCVar(MonoCVars.PersistPlanets) && FindPlanet(pod) != null;

    private void OnCryoEnter(EntityUid body, TransformComponent transform, CryosleepEnterEvent ev)
    {
        if (!_cfg.GetCVar(MonoCVars.PersistPlanets) || FindPlanet(ev.Cryopod) is not { } planet)
            return;

        if (ev.User is not { } user || !_profiles.TryGetValue(body, out var profile))
        {
            Log.Warning($"Could not save a cryopod reservation for {ToPrettyString(body)} on {planet.ID}: no player or character slot was tracked.");
            return;
        }

        var snapshot = string.Empty;
        if (planet.Persistence == PlanetPersistenceMode.Full && !TrySaveBody(body, planet, out snapshot))
            Log.Warning($"Saving a job-only cryopod reservation for {ToPrettyString(body)}.");

        var reservation = new CryopodReservation
        {
            Id = Guid.NewGuid(),
            User = user.UserId,
            Slot = profile,
            Job = TryComp<PlayerJobComponent>(body, out var job) ? job.JobPrototype?.Id : null,
            Body = snapshot,
        };

        RemoveReservations(user, profile);
        EnsureComp<CryopodRespawnComponent>(ev.Cryopod).Reservations.Add(reservation);
    }

    private void OnCryoWakeUp(EntityUid body, TransformComponent transform, CryosleepWakeUpEvent ev)
    {
        if (ev.User is { } user && _profiles.TryGetValue(body, out var profile))
            RemoveReservations(user, profile);
    }

    private void OnPodTerminating(Entity<CryopodRespawnComponent> pod, ref EntityTerminatingEvent ev)
    {
        if (_ticker.RunLevel != GameRunLevel.InRound || FindPlanet(pod, terminating: true) is not { } planet)
            return;

        foreach (var user in pod.Comp.Reservations.Select(record => record.User).Distinct())
        {
            if (_players.TryGetSessionById(new NetUserId(user), out var session) && session.Channel.IsConnected)
                _chat.DispatchServerMessage(session, Loc.GetString("cryopod-respawn-lost", ("planet", planet.Name)));
        }
    }

    private void RemoveReservations(NetUserId user, int profile)
    {
        foreach (var choice in FindReservations(user, profile))
            Comp<CryopodRespawnComponent>(choice.Pod).Reservations.Remove(choice.Reservation);
    }

    public void TrackBody(EntityUid body, NetUserId user, int slot)
    {
        _profiles[body] = slot;
        RemoveReservations(user, slot);
    }

    public int SelectedSlot(ICommonSession player) =>
        _preferences.TryGetCachedPreferences(player.UserId, out var prefs) ? prefs.SelectedCharacterIndex : -1;

    public HumanoidCharacterProfile? GetProfile(NetUserId user, int slot) =>
        _preferences.TryGetCachedPreferences(user, out var prefs) &&
        prefs.Characters.TryGetValue(slot, out var profile) ? profile as HumanoidCharacterProfile : null;

    public List<ReservationChoice> FindReservations(NetUserId user, int? profile = null)
    {
        var choices = new List<ReservationChoice>();
        if (profile < 0)
            return choices;

        var query = AllEntityQuery<CryopodRespawnComponent, CryoSleepComponent>();
        while (query.MoveNext(out var pod, out var spawn, out var cryo))
        {
            foreach (var reservation in spawn.Reservations)
            {
                if (reservation.User == user.UserId && (profile == null || reservation.Slot == profile) &&
                    FindPlanet(pod) is { } planet)
                    choices.Add(new(pod, planet, reservation, cryo.BodyContainer.ContainedEntity == null));
            }
        }

        return choices;
    }

    private PlanetTypePrototype? FindPlanet(EntityUid pod, bool terminating = false)
    {
        if ((!terminating && TerminatingOrDeleted(pod)) || Transform(pod).MapUid is not { } map ||
            TerminatingOrDeleted(map))
            return null;

        var planets = AllEntityQuery<PlanetBodyComponent>();
        while (planets.MoveNext(out var body))
        {
            if (TryComp<PlanetMapGenerationComponent>(body.SurfaceNetwork, out var state) &&
                state.Layers.ContainsValue(map) && _prototypes.TryIndex(body.Type, out var planet) &&
                planet.Persistence is PlanetPersistenceMode.Semi or PlanetPersistenceMode.Full &&
                planet.PersistenceFlags.Contains(PlanetPersistenceFlag.AllowRespawns))
                return planet;
        }

        return null;
    }

    public sealed record ReservationChoice(EntityUid Pod, PlanetTypePrototype Planet, CryopodReservation Reservation, bool Available);
}
