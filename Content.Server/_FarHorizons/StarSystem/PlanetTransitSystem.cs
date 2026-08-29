using System.Numerics;
using System.Linq;
using Content.Server._CE.ZLevels.Core;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Damage;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared.Mobs.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._FarHorizons.StarSystem;

public sealed partial class PlanetTransitSystem : SharedPlanetTransitSystem
{
    [Dependency] private CEZLevelsSystem _zLevels = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private DockingSystem _docking = default!;
    [Dependency] private ThrusterSystem _thruster = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan DepartureTime = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan ArrivalTime = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan AscentPrimingTime = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan AscentChargeTime = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AscentInputGap = TimeSpan.FromSeconds(0.25);
    private readonly Dictionary<EntityUid, (EntityUid Planet, EntityUid TopMap, TimeSpan LastPush)> _ascentPriming = new();
    private readonly Dictionary<EntityUid, (EntityUid Planet, EntityUid TopMap)> _pendingAscents = new();
    private readonly List<EntityUid> _expiredAscents = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<ShuttleConsoleComponent>(ShuttleConsoleUiKey.Key, subs =>
        {
            subs.Event<PlanetDescentRequestMessage>(OnDescentRequest);
        });

        SubscribeLocalEvent<MapGridComponent, CEZOpenSkyExitAttemptEvent>(OnOpenSkyExit);
        SubscribeLocalEvent<PlanetTransitComponent, ComponentShutdown>(OnTransitShutdown);
        SubscribeLocalEvent<PlanetTransitFailureComponent, ComponentShutdown>(OnFailureShutdown);
        SubscribeLocalEvent<ThrusterComponent, DamageChangedEvent>(OnThrusterDamaged);
    }

    private void OnDescentRequest(Entity<ShuttleConsoleComponent> ent, ref PlanetDescentRequestMessage args)
    {
        BeginPredictedDescent(ent, args);
    }

    private bool CanDescend(EntityUid grid, EntityUid planetUid, PlanetBodyComponent planet, EntityUid surface)
        => GetDescentAvailability(grid, planetUid, planet, surface, true) == DescentAvailability.Available;

    protected override void PredictedDescentStarted(EntityUid grid,
        PlanetTransitComponent transit,
        bool pilotLockAdded)
    {
        IsolateGrid(grid, transit);
        PrepareTransit(grid, transit, pilotLockAdded, false);
    }

    private void IsolateGrid(EntityUid grid, PlanetTransitComponent transit)
    {
        _docking.UndockDocks(grid);
        transit.Grids.Clear();
        transit.Grids.Add(grid);

        if (!HasComp<PreventDockingComponent>(grid))
        {
            AddComp<PreventDockingComponent>(grid);
            transit.OwnedDockLock = true;
        }

        Dirty(grid, transit);
    }

    private void OnOpenSkyExit(Entity<MapGridComponent> ent, ref CEZOpenSkyExitAttemptEvent args)
    {
        if (!args.Pushing ||
            !TryComp<CEZMapComponent>(args.TopMap, out var top) ||
            !TryComp<PlanetSurfaceComponent>(top.NetworkUid, out var surface) ||
            !TryComp<PlanetBodyComponent>(surface.Planet, out _))
        {
            return;
        }

        args.Handled = true;
        if (HasComp<PlanetTransitFailureComponent>(ent))
            return;

        var now = _timing.CurTime;
        if (TryComp<PlanetTransitComponent>(ent, out var active))
        {
            if (active.Direction != PlanetTransitDirection.Ascent ||
                active.Phase != PlanetTransitPhase.Priming ||
                !_ascentPriming.ContainsKey(ent))
            {
                return;
            }

            _ascentPriming[ent] = (surface.Planet, args.TopMap, now);
            if (now >= active.PhaseEnd)
                _pendingAscents[ent] = (surface.Planet, args.TopMap);
            return;
        }

        var set = _zLevels.CollectTransitSet(ent);
        var currentMap = Transform(ent).MapUid;
        if (currentMap == null || set.Any(grid => Transform(grid).MapUid != currentMap))
            return;

        var transit = AddComp<PlanetTransitComponent>(ent);
        transit.Planet = surface.Planet;
        transit.Direction = PlanetTransitDirection.Ascent;
        transit.Phase = PlanetTransitPhase.Priming;
        transit.PhaseStart = now;
        transit.PhaseEnd = now + AscentPrimingTime;
        transit.Grids.Add(ent);
        Dirty(ent, transit);
        _ascentPriming[ent] = (surface.Planet, args.TopMap, now);
    }

    private void PrepareTransit(EntityUid grid, PlanetTransitComponent transit, bool pilotLockAdded)
        => PrepareTransit(grid, transit, pilotLockAdded, true);

    private void PrepareTransit(EntityUid grid,
        PlanetTransitComponent transit,
        bool pilotLockAdded,
        bool includeSet)
    {
        transit.Prepared = true;
        if (includeSet)
            transit.Grids.UnionWith(_zLevels.CollectTransitSet(grid));
        if (pilotLockAdded)
            transit.OwnedPilotLocks.Add(grid);

        foreach (var member in transit.Grids)
        {
            if (!HasComp<PreventPilotComponent>(member))
            {
                AddComp<PreventPilotComponent>(member);
                transit.OwnedPilotLocks.Add(member);
            }

            if (TryComp<PhysicsComponent>(member, out var body) && body.BodyType == BodyType.Static)
                transit.InitiallyStatic.Add(member);

            _shuttle.Disable(member);
            if (TryComp<ShuttleComponent>(member, out var shuttle))
                _thruster.SetPlanetTransitVisuals(shuttle, true);

            if (body != null)
            {
                _physics.SetLinearVelocity(member, Vector2.Zero, body: body);
                _physics.SetAngularVelocity(member, 0f, body: body);
            }
        }

        Dirty(grid, transit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateFailures();
        UpdateAscentCharges();
        StartPendingAscents();

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<PlanetTransitComponent, MapGridComponent>();
        while (query.MoveNext(out var uid, out var transit, out var grid))
        {
            if (now < transit.PhaseEnd)
                continue;

            switch (transit.Phase)
            {
                case PlanetTransitPhase.Charging when transit.Direction == PlanetTransitDirection.Descent:
                    CommitDescent((uid, transit), (uid, grid));
                    break;
                case PlanetTransitPhase.Charging when transit.Direction == PlanetTransitDirection.Ascent:
                    CommitAscent((uid, transit), (uid, grid));
                    break;
                case PlanetTransitPhase.Departing:
                    Transfer((uid, transit), (uid, grid));
                    break;
                case PlanetTransitPhase.Arriving:
                    FinishArrival((uid, transit), (uid, grid));
                    break;
            }
        }
    }

    private void OnThrusterDamaged(Entity<ThrusterComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null || !ent.Comp.Enabled ||
            Transform(ent).GridUid is not { } thrusterGrid)
        {
            return;
        }

        var query = EntityQueryEnumerator<PlanetTransitComponent>();
        while (query.MoveNext(out var uid, out var transit))
        {
            if (transit.Phase != PlanetTransitPhase.Charging ||
                !transit.Grids.Contains(thrusterGrid))
                continue;

            AbortCharge((uid, transit));
            return;
        }
    }

    private void AbortCharge(Entity<PlanetTransitComponent> ent)
    {
        var grids = ent.Comp.Grids.ToArray();
        var pilotLocks = ent.Comp.OwnedPilotLocks.ToHashSet();
        ent.Comp.OwnedPilotLocks.Clear();
        var drop = ent.Comp.Direction == PlanetTransitDirection.Ascent &&
                   ent.Comp.Phase == PlanetTransitPhase.Charging;
        _ascentPriming.Remove(ent);
        _pendingAscents.Remove(ent);
        RemComp<PlanetTransitComponent>(ent);

        var now = _timing.CurTime;
        foreach (var grid in grids)
        {
            if (TerminatingOrDeleted(grid))
                continue;

            var failure = EnsureComp<PlanetTransitFailureComponent>(grid);
            failure.Start = now;
            failure.End = now + DriveRespoolTime;
            failure.OwnedPilotLock = pilotLocks.Contains(grid);

            if (!HasComp<PreventPilotComponent>(grid))
            {
                AddComp<PreventPilotComponent>(grid);
                failure.OwnedPilotLock = true;
            }

            Dirty(grid, failure);
            DisableThrusters(grid);
            if (TryComp<ShuttleComponent>(grid, out var shuttle))
                _thruster.SetPlanetTransitVisuals(shuttle, true);

            if (drop)
            {
                var gravgen = EnsureComp<CEZGravgenStunnedComponent>(grid);
                gravgen.End = failure.End;
            }
        }

        var mobs = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobs.MoveNext(out var mob, out _, out var xform))
        {
            if (xform.GridUid is { } grid && grids.Contains(grid))
                _stun.TryParalyze(mob, DischargeStunTime, true);
        }
    }

    private void UpdateFailures()
    {
        var query = EntityQueryEnumerator<PlanetTransitFailureComponent>();
        while (query.MoveNext(out var uid, out var failure))
        {
            if (_timing.CurTime >= failure.End)
            {
                RemCompDeferred<PlanetTransitFailureComponent>(uid);
                continue;
            }

            DisableThrusters(uid);
        }
    }

    private void DisableThrusters(EntityUid grid)
    {
        if (!TryComp<ShuttleComponent>(grid, out var shuttle))
            return;

        _thruster.DisableLinearThrusters(shuttle);
        _thruster.SetAngularThrust(shuttle, false);
    }

    private void OnFailureShutdown(Entity<PlanetTransitFailureComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (ent.Comp.OwnedPilotLock)
            RemCompDeferred<PreventPilotComponent>(ent);

        if (TryComp<ShuttleComponent>(ent, out var shuttle))
            _thruster.SetPlanetTransitVisuals(shuttle, false);

        RemCompDeferred<CEZGravgenStunnedComponent>(ent);
    }

    private void UpdateAscentCharges()
    {
        if (_ascentPriming.Count == 0)
            return;

        var now = _timing.CurTime;
        _expiredAscents.Clear();
        foreach (var (uid, priming) in _ascentPriming)
        {
            if (TerminatingOrDeleted(uid) ||
                !TryComp<PlanetTransitComponent>(uid, out var transit) ||
                transit.Direction != PlanetTransitDirection.Ascent ||
                transit.Phase != PlanetTransitPhase.Priming ||
                TerminatingOrDeleted(priming.Planet) ||
                now - priming.LastPush > AscentInputGap)
            {
                _expiredAscents.Add(uid);
                continue;
            }

            if (now >= transit.PhaseEnd)
                _pendingAscents[uid] = (priming.Planet, priming.TopMap);
        }

        foreach (var uid in _expiredAscents)
        {
            _ascentPriming.Remove(uid);
            if (!TerminatingOrDeleted(uid))
                RemComp<PlanetTransitComponent>(uid);
        }
    }

    private void StartPendingAscents()
    {
        if (_pendingAscents.Count == 0)
            return;

        foreach (var (uid, pending) in _pendingAscents)
        {
            if (TerminatingOrDeleted(uid) ||
                !HasComp<MapGridComponent>(uid) ||
                !TryComp<PlanetTransitComponent>(uid, out var transit) ||
                transit.Direction != PlanetTransitDirection.Ascent ||
                transit.Phase != PlanetTransitPhase.Priming ||
                !TryComp<PlanetBodyComponent>(pending.Planet, out _) ||
                Transform(uid).MapUid is not { } currentMap ||
                currentMap != pending.TopMap && !HasComp<CEZTransitMapComponent>(currentMap))
            {
                _ascentPriming.Remove(uid);
                if (!TerminatingOrDeleted(uid))
                    RemComp<PlanetTransitComponent>(uid);
                continue;
            }

            var set = _zLevels.CollectTransitSet(uid);
            if (set.Any(member => Transform(member).MapUid != currentMap))
                continue;

            _ascentPriming.Remove(uid);
            IsolateGrid(uid, transit);
            transit.OriginMap = pending.TopMap;
            PrepareTransit(uid, transit, false);
            SetPhase((uid, transit), PlanetTransitPhase.Charging, AscentChargeTime);
        }

        _pendingAscents.Clear();
    }

    private void CommitDescent(Entity<PlanetTransitComponent> ent, Entity<MapGridComponent> grid)
    {
        if (!TryComp<PlanetBodyComponent>(ent.Comp.Planet, out var planet) ||
            planet.SurfaceNetwork is not { } surface ||
            !CanDescend(grid, ent.Comp.Planet, planet, surface))
        {
            RemComp<PlanetTransitComponent>(ent);
            return;
        }

        IsolateGrid(ent.Owner, ent.Comp);
        if (Transform(grid).MapUid is not { } origin || !StartDeparture(ent, grid, origin))
            RemComp<PlanetTransitComponent>(ent);
    }

    private void CommitAscent(Entity<PlanetTransitComponent> ent, Entity<MapGridComponent> grid)
    {
        if (ent.Comp.OriginMap is not { } topMap ||
            !TryComp<PlanetBodyComponent>(ent.Comp.Planet, out _) ||
            Transform(grid).MapUid is not { } currentMap ||
            currentMap != topMap && !HasComp<CEZTransitMapComponent>(currentMap))
        {
            RemComp<PlanetTransitComponent>(ent);
            return;
        }

        IsolateGrid(ent.Owner, ent.Comp);
        if (!StartDeparture(ent, grid, topMap))
            RemComp<PlanetTransitComponent>(ent);
    }

    private bool StartDeparture(Entity<PlanetTransitComponent> ent,
        Entity<MapGridComponent> grid,
        EntityUid originMap)
    {
        if (!_zLevels.TryCreateDetachedTransit(grid, out var transitMap))
            return false;

        ent.Comp.TransitMap = transitMap;
        ent.Comp.OriginMap = originMap;
        SetPhase(ent, PlanetTransitPhase.Departing, DepartureTime);

        var visual = AddComp<PlanetTransitMapComponent>(transitMap);
        visual.OriginMap = originMap;
        visual.Grid = ent;
        visual.Direction = ent.Comp.Direction;
        visual.Start = ent.Comp.PhaseStart;
        visual.End = ent.Comp.PhaseEnd;
        Dirty(transitMap, visual);
        return true;
    }

    private void Transfer(Entity<PlanetTransitComponent> ent, Entity<MapGridComponent> grid)
    {
        if (ent.Comp.Direction == PlanetTransitDirection.Ascent)
        {
            if (!BeginAscentArrival(ent, grid))
            {
                RestoreOrigin(ent, grid);
                RemComp<PlanetTransitComponent>(ent);
            }
            return;
        }

        var success = ent.Comp.Direction switch
        {
            PlanetTransitDirection.Descent => TransferDescent(ent, grid),
            _ => false,
        };

        if (!success)
        {
            RestoreOrigin(ent, grid);
            RemComp<PlanetTransitComponent>(ent);
            return;
        }

        ent.Comp.Transferred = true;
        DeleteTransitMap(ent.Comp);
        SetPhase(ent, PlanetTransitPhase.Arriving, ArrivalTime);
    }

    private bool BeginAscentArrival(Entity<PlanetTransitComponent> ent, Entity<MapGridComponent> grid)
    {
        if (!TryComp<PlanetBodyComponent>(ent.Comp.Planet, out var planet) ||
            Transform(ent.Comp.Planet).MapUid is not { } spaceMap ||
            ent.Comp.TransitMap is not { } transitMap ||
            !TryComp<PlanetTransitMapComponent>(transitMap, out var visual))
        {
            return false;
        }

        var center = _transform.GetWorldPosition(ent.Comp.Planet);
        var target = PickPoint(center, planet.ApproachRadius);
        if (!_zLevels.TryRecenterDetachedTransit(grid, target))
            return false;

        SetPhase(ent, PlanetTransitPhase.Arriving, ArrivalTime);
        visual.OriginMap = spaceMap;
        visual.Start = ent.Comp.PhaseStart;
        visual.End = ent.Comp.PhaseEnd;
        visual.Arrival = true;
        Dirty(transitMap, visual);
        _zLevels.QueueAllViewerUpdates();
        return true;
    }

    private void FinishArrival(Entity<PlanetTransitComponent> ent, Entity<MapGridComponent> grid)
    {
        if (ent.Comp.Direction == PlanetTransitDirection.Ascent)
        {
            if (ent.Comp.TransitMap is not { } transitMap ||
                !TryComp<PlanetTransitMapComponent>(transitMap, out var visual) ||
                Transform(grid).MapUid != transitMap ||
                !_zLevels.TryMoveDetachedTransit(grid, visual.OriginMap, _transform.GetWorldPosition(grid)))
            {
                return;
            }

            ent.Comp.Transferred = true;
        }

        DeleteTransitMap(ent.Comp);
        RemComp<PlanetTransitComponent>(ent);
    }

    private bool TransferDescent(Entity<PlanetTransitComponent> ent, Entity<MapGridComponent> grid)
    {
        if (!TryComp<PlanetBodyComponent>(ent.Comp.Planet, out var planet) ||
            planet.SurfaceNetwork is not { } networkUid ||
            !TryComp<CEZMapNetworkComponent>(networkUid, out var network) ||
            !_prototypes.TryIndex(planet.Type, out var planetProto))
        {
            return false;
        }

        var target = PickPoint(Vector2.Zero, planetProto.LandingRadius);
        return _zLevels.TryInsertIntoOpenSky(grid, (networkUid, network), target);
    }

    private Vector2 PickPoint(Vector2 center, float radius)
    {
        if (radius <= 0f)
            return center;

        return center + _random.NextAngle().ToVec() * (radius * MathF.Sqrt(_random.NextFloat()));
    }

    private void SetPhase(Entity<PlanetTransitComponent> ent, PlanetTransitPhase phase, TimeSpan duration)
    {
        ent.Comp.Phase = phase;
        ent.Comp.PhaseStart = _timing.CurTime;
        ent.Comp.PhaseEnd = _timing.CurTime + duration;
        Dirty(ent);
    }

    private void OnTransitShutdown(Entity<PlanetTransitComponent> ent, ref ComponentShutdown args)
    {
        _ascentPriming.Remove(ent);
        _pendingAscents.Remove(ent);

        if (ent.Comp.OwnedDockLock && !TerminatingOrDeleted(ent))
            RemCompDeferred<PreventDockingComponent>(ent);

        if (!ent.Comp.Prepared)
            return;

        if (!ent.Comp.Transferred && TryComp<MapGridComponent>(ent, out var grid))
            RestoreOrigin(ent, (ent, grid));

        DeleteTransitMap(ent.Comp);
        foreach (var member in ent.Comp.Grids)
        {
            if (TerminatingOrDeleted(member))
                continue;

            if (TryComp<ShuttleComponent>(member, out var shuttle))
                _thruster.SetPlanetTransitVisuals(shuttle, false);

            if (ent.Comp.Transferred || !ent.Comp.InitiallyStatic.Contains(member))
                _shuttle.Enable(member);
        }

        foreach (var member in ent.Comp.OwnedPilotLocks)
        {
            if (!TerminatingOrDeleted(member))
                RemCompDeferred<PreventPilotComponent>(member);
        }

    }

    private void RestoreOrigin(Entity<PlanetTransitComponent> ent, Entity<MapGridComponent> grid)
    {
        if (ent.Comp.TransitMap is not { } map ||
            Transform(grid).MapUid != map ||
            ent.Comp.OriginMap is not { } origin ||
            TerminatingOrDeleted(origin))
        {
            return;
        }

        _zLevels.TryMoveDetachedTransit(grid, origin, _transform.GetWorldPosition(grid));
    }

    private void DeleteTransitMap(PlanetTransitComponent transit)
    {
        if (transit.TransitMap is { } map && !TerminatingOrDeleted(map))
            QueueDel(map);

        transit.TransitMap = null;
    }
}
