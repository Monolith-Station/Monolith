using System.Linq;
using System.Numerics;
using Content.Server._CE.Planets.Shields;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._CE.Planets.Hellpods;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._Mono.CCVar;
using Content.Shared._Mono.Planets;
using Content.Shared._Mono.Ships;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server._CE.Planets.Hellpods;

public sealed partial class CEHellpodSystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private SharedShipShieldShellSystem _shields = default!;
    [Dependency] private CEPlanetShieldSystem _planetShield = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IConfigurationManager _config = default!;

    private static readonly TimeSpan DepartureTime = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan ConfirmGrace = TimeSpan.FromSeconds(5);
    private readonly Dictionary<EntityUid, Drop> _drops = new();
    private readonly List<Flight> _flights = new();
    private TimeSpan _nextConsoleUpdate;

    private sealed record Drop(EntityUid Console, EntityUid Actor, EntityUid Planet, Vector2 Center,
        float Radius, TimeSpan ConfirmAt)
    {
        public VolleyReservation? Reservation;
        public bool HoldCompleted;
        public TimeSpan? LaunchAt;
        public TimeSpan? FireAt;
        public bool RestoreDynamic;
        public bool OwnedDockLock;
    }

    private sealed record Flight(EntityUid Map, EntityUid Planet, EntityUid Top, TimeSpan End,
        List<(EntityUid Pod, EntityUid Proxy, Vector2 Target, Vector2 Entry)> Pods)
    {
        public bool Crossed;
    }

    private void InitializeConsole()
    {
        Subs.BuiEvents<CEHellpodConsoleComponent>(CEHellpodConsoleKey.Key, subs =>
        {
            subs.Event<CEHellpodConfirmMessage>(OnConfirm);
            subs.Event<BoundUIOpenedEvent>(OnConsoleOpened);
        });
    }

    private void OnConsoleOpened(Entity<CEHellpodConsoleComponent> ent, ref BoundUIOpenedEvent args) => PublishConsole(ent);

    private bool IsBoarding(EntityUid hatch, EntityUid? grid) =>
        grid is { } uid && _drops.TryGetValue(uid, out var drop) && drop.FireAt == null &&
        drop.LaunchAt > _timing.CurTime && drop.Reservation?.Hatches.Contains(hatch) == true;

    private bool TryGetApproach(EntityUid console, out EntityUid grid, out EntityUid planet,
        out CEZMapNetworkComponent network)
    {
        grid = default;
        planet = default;
        network = default!;
        if (!TryComp<TransformComponent>(console, out var xform) || xform.GridUid is not { } shuttle ||
            !HasComp<ShuttleComponent>(shuttle) || xform.MapUid is not { } map ||
            HasComp<CEZMapComponent>(map) || HasComp<CEZTransitMapComponent>(map) ||
            HasComp<PlanetTransitMapComponent>(map) || HasComp<FTLComponent>(shuttle))
            return false;

        grid = shuttle;
        var position = _transform.GetWorldPosition(shuttle);
        var nearest = float.PositiveInfinity;
        var query = EntityQueryEnumerator<PlanetBodyComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var body, out var target))
        {
            if (target.MapUid != map || !TryComp(body.SurfaceNetwork, out CEZMapNetworkComponent? candidate))
                continue;

            var distance = Vector2.DistanceSquared(position, _transform.GetWorldPosition(target));
            if (distance > body.ApproachRadius * body.ApproachRadius || distance >= nearest)
                continue;

            nearest = distance;
            planet = uid;
            network = candidate;
        }

        return planet.IsValid();
    }

    public bool CanPreviewTerrain(EntityUid console, EntityUid actor, EntityUid map) =>
        _ui.IsUiOpen(console, CEHellpodConsoleKey.Key, actor) &&
        TryGetApproach(console, out _, out _, out var network) &&
        _levels.TryGetMapNetwork(map, out var mapNetwork) && mapNetwork.Owner == network.Owner;

    private EntityUid GetTerrain(CEZMapNetworkComponent network) =>
        network.SortedZLevels.FirstOrDefault(map => HasComp<BiomeComponent>(map));

    private float GetLimit(EntityUid terrain) =>
        TryComp<ToroidalMapComponent>(terrain, out var torus) ? torus.Size / 2f : 4096f;

    private void OnConfirm(Entity<CEHellpodConsoleComponent> ent, ref CEHellpodConfirmMessage args)
    {
        if (args.Action is CEHellpodHoldAction.Cancel or CEHellpodHoldAction.Complete)
        {
            if (Transform(ent).GridUid is { } current && _drops.TryGetValue(current, out var previous) &&
                previous.Console == ent.Owner && previous.Actor == args.Actor && previous.LaunchAt == null)
            {
                if (args.Action == CEHellpodHoldAction.Complete)
                    previous.HoldCompleted = true;
                else if (!previous.HoldCompleted)
                    EndDrop(current, previous);
            }
            else if (args.Action == CEHellpodHoldAction.Complete)
            {
                RejectConfirm(ent, args.Actor);
            }

            return;
        }

        if (args.Action != CEHellpodHoldAction.Start ||
            !TryGetApproach(ent, out var grid, out var planet, out var network) ||
            _drops.ContainsKey(grid) || HasComp<PreventPilotComponent>(grid) ||
            !float.IsFinite(args.Center.X) || !float.IsFinite(args.Center.Y) ||
            !float.IsFinite(args.Radius) || args.Radius is < CEHellpodConsoleComponent.MinRadius or > CEHellpodConsoleComponent.MaxRadius)
            return;

        var limit = GetLimit(GetTerrain(network)) - args.Radius;
        if (MathF.Abs(args.Center.X) > limit || MathF.Abs(args.Center.Y) > limit ||
            !_prototypes.TryIndex(ent.Comp.PodType, out var type) ||
            !CanFundVolley(GetAvailableSilo(ent), type, GetHatches(grid).Count))
            return;

        _drops.Add(grid, new Drop(ent, args.Actor, planet, args.Center, args.Radius,
            _timing.CurTime + CEHellpodConsoleComponent.ConfirmTime));
    }

    private void RejectConfirm(EntityUid console, EntityUid actor) =>
        _ui.ServerSendUiMessage(console, CEHellpodConsoleKey.Key, new CEHellpodConfirmRejectedMessage(), actor);

    private List<EntityUid> GetHatches(EntityUid grid)
    {
        var hatches = new List<EntityUid>();
        var query = EntityQueryEnumerator<CEHellpodHatchComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == grid && xform.Anchored)
                hatches.Add(uid);
        }
        return hatches;
    }

    private void UpdateConsole()
    {
        var now = _timing.CurTime;
        foreach (var (grid, drop) in _drops)
        {
            if (!HasComp<CEHellpodConsoleComponent>(drop.Console) ||
                !TryGetApproach(drop.Console, out var currentGrid, out var planet, out var network) ||
                currentGrid != grid || planet != drop.Planet)
            {
                EndDrop(grid, drop);
                continue;
            }

            if (drop.LaunchAt == null)
            {
                if (!_ui.IsUiOpen(drop.Console, CEHellpodConsoleKey.Key, drop.Actor) || HasComp<PreventPilotComponent>(grid))
                {
                    EndDrop(grid, drop);
                    continue;
                }
                if (now < drop.ConfirmAt)
                    continue;

                if (!drop.HoldCompleted)
                {
                    if (now >= drop.ConfirmAt + ConfirmGrace)
                        EndDrop(grid, drop);
                    continue;
                }

                if (!TryReserveVolley(drop, GetHatches(grid)))
                {
                    EndDrop(grid, drop);
                    continue;
                }

                AddComp<PreventPilotComponent>(grid);
                if (!HasComp<PreventDockingComponent>(grid))
                {
                    AddComp<PreventDockingComponent>(grid);
                    drop.OwnedDockLock = true;
                }
                drop.RestoreDynamic = TryComp<PhysicsComponent>(grid, out var body) && body.BodyType == BodyType.Dynamic;
                _shuttle.Disable(grid, force: true);
                _physics.SetLinearVelocity(grid, Vector2.Zero);
                _physics.SetAngularVelocity(grid, 0f);
                drop.LaunchAt = now + CEHellpodConsoleComponent.BoardingTime;
                PublishConsole(drop.Console);
            }

            var reservation = drop.Reservation!;
            var hatches = reservation.Hatches;
            for (var i = hatches.Count - 1; i >= 0; i--)
            {
                var hatch = hatches[i];
                if (!TerminatingOrDeleted(hatch) && HasComp<CEHellpodHatchComponent>(hatch) &&
                    Transform(hatch) is { Anchored: true } xform && xform.GridUid == grid)
                    continue;

                hatches.RemoveAt(i);
                UnloadPod(hatch);
            }

            if (drop.FireAt == null)
            {
                var occupied = hatches.Count(hatch => Seat(hatch).ContainedEntity != null);
                if (now < drop.LaunchAt && occupied < hatches.Count)
                    continue;
                if (occupied == 0)
                {
                    EndDrop(grid, drop);
                    continue;
                }
                drop.FireAt = now + CEHellpodConsoleComponent.LaunchDelay;
                PublishConsole(drop.Console);
            }

            if (now < drop.FireAt)
                continue;

            LaunchSpaceDrop(grid, drop, network, reservation);
            EndDrop(grid, drop);
        }

        UpdateFlights(now);
        if (now < _nextConsoleUpdate)
            return;
        _nextConsoleUpdate = now + TimeSpan.FromSeconds(0.35);
        var consoles = EntityQueryEnumerator<CEHellpodConsoleComponent>();
        while (consoles.MoveNext(out var uid, out _))
            PublishConsole(uid);
    }

    private void EndDrop(EntityUid grid, Drop drop)
    {
        _drops.Remove(grid);
        if (drop.HoldCompleted && drop.LaunchAt == null && _ui.IsUiOpen(drop.Console, CEHellpodConsoleKey.Key, drop.Actor))
            RejectConfirm(drop.Console, drop.Actor);
        if (drop.Reservation is { } reservation)
        {
            foreach (var hatch in reservation.Hatches)
                UnloadPod(hatch);
            RefundVolley(grid, drop, reservation);
        }
        PublishConsole(drop.Console);
        if (drop.LaunchAt == null || TerminatingOrDeleted(grid))
            return;
        RemComp<PreventPilotComponent>(grid);
        if (drop.OwnedDockLock)
            RemComp<PreventDockingComponent>(grid);
        if (drop.RestoreDynamic)
            _shuttle.Enable(grid, force: true);
    }

    private void PublishConsole(EntityUid console)
    {
        if (TerminatingOrDeleted(console) ||
            !TryComp<CEHellpodConsoleComponent>(console, out var component))
            return;

        var silo = GetAvailableSilo(console);
        var state = new CEHellpodConsoleState
        {
            PodType = component.PodType,
            SiloConnected = silo != null,
        };
        if (Transform(console).GridUid is { } shuttle && HasComp<ShuttleComponent>(shuttle))
        {
            var hatches = GetHatches(shuttle);
            state.Hatches = hatches.Count;
            state.Occupied = hatches.Count(hatch => Seat(hatch).ContainedEntity != null);
            state.CanFundVolley = _prototypes.TryIndex(component.PodType, out var type) &&
                                 CanFundVolley(silo, type, state.Hatches);
        }

        if (TryGetApproach(console, out var grid, out var planet, out var network))
        {
            var terrain = GetTerrain(network);
            state.Planet = Name(planet);
            state.PlanetShielded = _planetShield.IsShielded(planet);
            state.Terrain = GetNetEntity(terrain);
            state.Limit = GetLimit(terrain);
            state.MinDepth = network.SortedMin;
            state.MaxDepth = network.SortedMax;
            state.GroundDepth = TryComp<CEZMapComponent>(terrain, out var ground) ? ground.Depth : network.SortedMin;
            if (_drops.TryGetValue(grid, out var drop))
            {
                state.ConfirmAt = drop.ConfirmAt;
                state.LaunchAt = drop.LaunchAt;
                state.FireAt = drop.FireAt;
                state.Center = drop.Center;
                state.Radius = drop.Radius;
            }
            state.Contacts = GetContacts(network.Owner, state.Limit);
        }
        if (!_ui.TryGetUiState<CEHellpodConsoleState>(console, CEHellpodConsoleKey.Key, out var previous) ||
            !SameReadout(previous, state))
            _ui.SetUiState(console, CEHellpodConsoleKey.Key, state);
    }

    private static bool SameReadout(CEHellpodConsoleState a, CEHellpodConsoleState b) =>
        a.PodType == b.PodType && a.SiloConnected == b.SiloConnected && a.CanFundVolley == b.CanFundVolley &&
        a.Planet == b.Planet && a.PlanetShielded == b.PlanetShielded && a.Terrain == b.Terrain &&
        a.MinDepth == b.MinDepth && a.MaxDepth == b.MaxDepth && a.GroundDepth == b.GroundDepth && a.Limit == b.Limit &&
        a.Occupied == b.Occupied && a.Hatches == b.Hatches &&
        a.ConfirmAt == b.ConfirmAt && a.LaunchAt == b.LaunchAt && a.FireAt == b.FireAt &&
        a.Center == b.Center && a.Radius == b.Radius && a.Contacts.AsSpan().SequenceEqual(b.Contacts);

    private CEHellpodContact[] GetContacts(EntityUid network, float limit)
    {
        var contacts = new List<CEHellpodContact>();
        var grids = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (grids.MoveNext(out var uid, out var grid, out var xform))
        {
            if (uid == xform.MapUid || _shields.GetStack(xform.MapUid) != network)
                continue;
            var center = Vector2.Transform(grid.LocalAABB.Center, _transform.GetWorldMatrix(xform));
            var radii = grid.LocalAABB.Size / 2f;
            if (MathF.Max(MathF.Abs(center.X), MathF.Abs(center.Y)) > limit + radii.Length())
                continue;
            contacts.Add(new(center, radii, _transform.GetWorldRotation(xform),
                _levels.GetAbsoluteAltitude(uid), 0.5f, false));
        }

        var shields = EntityQueryEnumerator<ShipShieldProjectorComponent, TransformComponent>();
        while (shields.MoveNext(out var uid, out var shield, out var xform))
        {
            if (_shields.GetStack(xform.MapUid) != network || !_shields.IsSolid(shield))
                continue;
            var radii = shield.Shield.Radii;
            var center = _transform.GetWorldPosition(xform);
            if (MathF.Max(MathF.Abs(center.X), MathF.Abs(center.Y)) > limit + MathF.Max(radii.X, radii.Y))
                continue;
            contacts.Add(new(center, new Vector2(radii.X, radii.Y),
                _transform.GetWorldRotation(xform), _levels.GetAbsoluteAltitude(uid) + shield.Shield.CenterHeight,
                radii.Z, true));
        }
        return contacts.ToArray();
    }

    private void LaunchSpaceDrop(EntityUid grid, Drop drop, CEZMapNetworkComponent network, VolleyReservation reservation)
    {
        var top = network.SortedZLevels.LastOrDefault(uid => HasComp<MapComponent>(uid));
        if (!top.IsValid() || !reservation.Hatches.Any(hatch => Seat(hatch).ContainedEntity != null))
            return;

        var map = _maps.CreateMap(out var mapId);
        if (TryComp<MapLightComponent>(top, out var topLight))
            EnsureComp<MapLightComponent>(map).AmbientLightColor = topLight.AmbientLightColor;
        var visual = AddComp<CEHellpodApproachComponent>(map);
        visual.Planet = drop.Planet;
        visual.FormationCenter = _transform.GetWorldPosition(grid);
        var planetPosition = _transform.GetWorldPosition(drop.Planet);
        var parallax = 0f;
        var projectedRadius = 0f;
        if (TryComp<PlanetBodyComponent>(drop.Planet, out var body) &&
            TryComp<StarSystemMapComponent>(body.StarSystemMap, out var starMap) &&
            starMap.StarSystem is { } system && (uint) body.Index < (uint) system.Planets.Count)
        {
            var planet = system.Planets[body.Index];
            var projection = planet.GetProjection();
            parallax = projection.Parallax;
            projectedRadius = planet.Radius * projection.RadiusScale;
            visual.SurfacePoint = (visual.FormationCenter - planetPosition) * (parallax / MathF.Max(projectedRadius, 0.01f));
        }
        visual.Start = _timing.CurTime;
        visual.End = visual.Start + DepartureTime;
        Dirty(map, visual);
        var pods = new List<(EntityUid Pod, EntityUid Proxy, Vector2 Target, Vector2 Entry)>();
        var opening = visual.SurfacePoint / MathF.Max(1f, visual.SurfacePoint.Length() / 0.82f);
        var ricochetChance = Math.Clamp(_config.GetCVar(MonoCVars.HellpodRicochetPercent), 0f, 100f) / 100f;
        foreach (var hatch in reservation.Hatches)
        {
            if (Seat(hatch).ContainedEntity is not { } occupant)
                continue;

            var prototype = reservation.Pod;
            var target = drop.Center + _random.NextAngle().ToVec() * (drop.Radius * MathF.Sqrt(_random.NextFloat()));
            var origin = _transform.GetMapCoordinates(hatch);
            var pod = Spawn(prototype, new MapCoordinates(origin.Position, mapId));
            if (!_containers.Insert(occupant, Seat(pod)))
            {
                QueueDel(pod);
                continue;
            }

            reservation.Unfired--;
            var entry = opening + (target - drop.Center) * (0.16f / drop.Radius);
            var component = Comp<CEHellpodComponent>(pod);
            component.InFlight = true;
            component.OrbitalEntry = entry;
            if (ricochetChance > 0f && _random.Prob(ricochetChance))
                component.Ricochet = new(GetRicochetExit(origin.Position, planetPosition, entry, parallax, projectedRadius));
            Dirty(pod, component);
            var proxy = Spawn(prototype, origin);
            _physics.SetCanCollide(proxy, false);
            RemComp<CEZPhysicsComponent>(proxy);
            var departure = AddComp<CEHellpodDepartureComponent>(proxy);
            departure.Start = visual.Start;
            departure.End = visual.End;
            departure.Planet = drop.Planet;
            departure.Entry = entry;
            departure.Ricochet = component.Ricochet;
            Dirty(proxy, departure);
            pods.Add((pod, proxy, target, entry));
        }
        if (pods.Count == 0)
        {
            QueueDel(map);
            return;
        }

        _flights.Add(new Flight(map, drop.Planet, top, visual.End, pods));
    }

    private static Vector2 GetRicochetExit(Vector2 origin, Vector2 planet, Vector2 entry, float parallax, float radius)
    {
        var center = Vector2.Lerp(origin, planet, parallax);
        var impact = center + entry * radius;
        var incoming = impact - origin;
        var distance = incoming.Length();
        var direction = distance > 0.01f ? incoming / distance : -Vector2.UnitY;
        return impact + direction * (distance + 24f);
    }

    private void UpdateFlights(TimeSpan now)
    {
        for (var i = _flights.Count - 1; i >= 0; i--)
        {
            var flight = _flights[i];
            if (now < flight.End)
                continue;

            if (!flight.Crossed)
            {
                flight.Crossed = true;
                var shielded = !TerminatingOrDeleted(flight.Top) && _planetShield.IsShielded(flight.Planet);
                foreach (var (pod, proxy, _, entry) in flight.Pods)
                {
                    if (TerminatingOrDeleted(pod) || !TryComp<CEHellpodComponent>(pod, out var component))
                        continue;

                    if (shielded)
                        _planetShield.RecordImpact(flight.Planet, entry, flight.End,
                            component.Ricochet != null ? CEHellpodComponent.RicochetRippleStrength : 1f);

                    if (component.Ricochet is { } ricochet)
                    {
                        component.Ricochet = shielded ? ricochet with { Confirmed = true } : null;
                        Dirty(pod, component);
                        if (TryComp<CEHellpodDepartureComponent>(proxy, out var departure))
                        {
                            departure.Ricochet = component.Ricochet;
                            Dirty(proxy, departure);
                        }
                    }

                    if (shielded && component.Ricochet == null)
                        LoseIntegrity(component, CEHellpodComponent.ShieldIntegrityLoss);
                }
            }

            for (var j = flight.Pods.Count - 1; j >= 0; j--)
            {
                var (pod, proxy, target, _) = flight.Pods[j];
                if (!TryComp<CEHellpodComponent>(pod, out var component) || TerminatingOrDeleted(pod))
                {
                    flight.Pods.RemoveAt(j);
                    QueueDel(proxy);
                    continue;
                }
                if (component.Ricochet is { Confirmed: true } ricochet)
                {
                    if (now < flight.End + DepartureTime)
                        continue;

                    if (!TerminatingOrDeleted(proxy))
                        _transform.SetMapCoordinates(pod, new MapCoordinates(ricochet.Exit, Transform(proxy).MapID));

                    FinishPod((pod, component));
                }
                else if (!TerminatingOrDeleted(flight.Top))
                {
                    component.OrbitalArrivalAt = flight.End;
                    Dirty(pod, component);
                    _transform.SetMapCoordinates(pod, new MapCoordinates(target, Transform(flight.Top).MapID));
                    _transform.SetWorldRotation(pod, _transform.GetWorldRotation(flight.Top));
                    _levels.SetZPosition(pod, 1f);
                    _levels.SetZVelocity(pod, -component.DropSpeed);
                }
                else if (!TerminatingOrDeleted(proxy))
                {
                    _transform.SetMapCoordinates(pod, _transform.GetMapCoordinates(proxy));
                    component.InFlight = false;
                    if (Seat(pod).ContainedEntity is { } rider)
                        _containers.Remove(rider, Seat(pod));
                    QueueDel(pod);
                }
                QueueDel(proxy);
                flight.Pods.RemoveAt(j);
            }

            if (flight.Pods.Count == 0)
            {
                _flights.RemoveAt(i);
                QueueDel(flight.Map);
            }
        }
    }
}
