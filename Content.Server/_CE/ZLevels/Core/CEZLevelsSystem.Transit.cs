/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using System.Numerics;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._FarHorizons.StarSystem;
using Robust.Server.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private DockingSystem _dockSystem = default!;
    [Dependency] private ShuttleConsoleSystem _console = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;

    private void InitializeTransit()
    {
        SubscribeLocalEvent<GridAddEvent>(OnGridAdd);
        SubscribeLocalEvent<MapGridComponent, EntParentChangedMessage>(OnGridParentChanged);
    }

    private bool HasTransitMapReference(EntityUid map)
    {
        var gaps = AllEntityQuery<CEZTransitMapComponent>();
        while (gaps.MoveNext(out var uid, out var gap))
        {
            if ((gap.LowerMap == map || gap.UpperMap == map) &&
                !TerminatingOrDeleted(uid) && !EntityManager.IsQueuedForDeletion(uid))
                return true;
        }

        var visuals = AllEntityQuery<PlanetTransitMapComponent>();
        while (visuals.MoveNext(out var uid, out var visual))
        {
            if (visual.OriginMap == map && !TerminatingOrDeleted(uid) && !EntityManager.IsQueuedForDeletion(uid))
                return true;
        }

        var ships = AllEntityQuery<PlanetTransitComponent>();
        while (ships.MoveNext(out var uid, out var ship))
        {
            if (ship.OriginMap == map && !TerminatingOrDeleted(uid) && !EntityManager.IsQueuedForDeletion(uid))
                return true;
        }

        return false;
    }

    private void RefreshTransitMapLinks(Entity<CEZMapNetworkComponent> network, (EntityUid Map, int Depth)? removedMap)
    {
        var query = AllEntityQuery<CEZTransitMapComponent>();
        while (query.MoveNext(out var uid, out var gap))
        {
            if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid))
                continue;

            if (removedMap is { } removed && (gap.LowerMap == removed.Map || gap.UpperMap == removed.Map))
            {
                RecoverTransitAnchor((uid, gap), network, removed.Map, removed.Depth);
            }
            else if ((!gap.AutoLand || gap.UpperMap != null) &&
                     TryGetLiveTransitAnchor(gap.LowerMap, out var lower) && lower.Comp.NetworkUid == network.Owner &&
                     lower.Comp.MapAbove != gap.UpperMap &&
                     TryGetLiveTransitAnchor(lower.Comp.MapAbove, out var upper))
            {
                ReanchorTransit((uid, gap), lower, upper);
            }
        }
    }

    private bool TryGetLiveTransitAnchor(EntityUid? map, out Entity<CEZMapComponent> anchor)
    {
        if (map is { } uid && !TerminatingOrDeleted(uid) && !EntityManager.IsQueuedForDeletion(uid) &&
            TryComp<CEZMapComponent>(uid, out var level))
        {
            anchor = (uid, level);
            return true;
        }

        anchor = default;
        return false;
    }

    private void RecoverTransitAnchor(Entity<CEZTransitMapComponent> gap, Entity<CEZMapNetworkComponent> network,
        EntityUid removed, int depth)
    {
        Entity<CEZMapComponent>? below = null;
        Entity<CEZMapComponent>? above = null;
        foreach (var map in network.Comp.SortedZLevels)
        {
            if (!TryGetLiveTransitAnchor(map, out var anchor))
                continue;

            if (anchor.Comp.Depth > depth)
            {
                above = anchor;
                break;
            }

            below = anchor;
        }

        var lostLower = gap.Comp.LowerMap == removed;
        if (lostLower && gap.Comp.AutoLand && gap.Comp.UpperMap == null && (below ?? above) is { } entry)
        {
            ReanchorTransit(gap, entry, null);
            return;
        }

        if (lostLower && below is { } newLower && TryGetLiveTransitAnchor(gap.Comp.UpperMap, out var upper))
        {
            ReanchorTransit(gap, newLower, upper);
            return;
        }

        if (!lostLower && above is { } newUpper && TryGetLiveTransitAnchor(gap.Comp.LowerMap, out var lower))
        {
            ReanchorTransit(gap, lower, newUpper);
            return;
        }

        var other = lostLower ? gap.Comp.UpperMap : gap.Comp.LowerMap;
        var destination = TryGetLiveTransitAnchor(other, out var surviving) ? surviving : below ?? above;
        if (destination is not { } target)
        {
            Log.Warning($"Transit {gap.Owner} lost its anchors and there is literally nowhere to put it in the stack. Giving up and erasing it from existence.");
            QueueDel(gap);
            return;
        }

        var grids = CollectGridsOnMap(gap);
        MoveGridSetToMap(grids, target, target.Comp.Depth - depth, target.Comp.Depth);
        foreach (var grid in grids)
            ResetGridTransitState(grid);

        QueueDel(gap);
        Log.Warning($"Transit {gap.Owner} lost an anchor; moved grids to {target.Owner}.");
    }

    private void ReanchorTransit(Entity<CEZTransitMapComponent> gap, Entity<CEZMapComponent> lower,
        Entity<CEZMapComponent>? upper)
    {
        var oldSpan = GetTransitSpan(gap.Comp);
        var oldDepth = TryComp<CEZMapComponent>(gap.Comp.LowerMap, out var previousLower)
            ? previousLower.Depth
            : lower.Comp.Depth;
        var altitude = oldDepth + GetTransitProgress(gap.Comp) * oldSpan;

        gap.Comp.LowerMap = lower;
        gap.Comp.UpperMap = upper?.Owner;
        Dirty(gap, gap.Comp);
        _transitLastProgress.Remove(gap);

        var span = GetTransitSpan(gap.Comp);
        var progress = Math.Clamp((altitude - lower.Comp.Depth) / span, 0f, 1f);
        foreach (var grid in CollectGridsOnMap(gap))
            SetZPosition(grid, progress);
    }

    private void OnGridAdd(GridAddEvent ev)
    {
        RefreshGridZPhysics(ev.EntityUid);
    }

    private void OnGridParentChanged(Entity<MapGridComponent> ent, ref EntParentChangedMessage args)
    {
        RefreshGridZPhysics(ent);
    }

    private void SweepMapGridsForZPhysics(EntityUid mapUid)
    {
        var children = Transform(mapUid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (HasComp<MapGridComponent>(child))
                RefreshGridZPhysics(child);
        }
    }

    private void RefreshGridZPhysics(EntityUid grid)
    {
        if (HasComp<MapComponent>(grid) || TerminatingOrDeleted(grid))
            return;

        var mapUid = Transform(grid).MapUid;
        var onZLevels = HasComp<CEZMapComponent>(mapUid) || HasComp<CEZTransitMapComponent>(mapUid);

        if (!onZLevels)
        {
            RemComp<CEZGridFallerComponent>(grid);
            return;
        }

        EnsureComp<CEZPhysicsComponent>(grid);

        if (!HasComp<CEZGridFallerComponent>(grid))
        {
            var faller = AddComp<CEZGridFallerComponent>(grid);
            faller.GravityTime = _timing.CurTime + TimeSpan.FromSeconds(faller.GridGravityGraceSeconds);
        }
    }

    /// <inheritdoc/>
    protected override bool TryMoveGrid(Entity<MapGridComponent> grid,
        Entity<CEZMapComponent, MapComponent> targetMap,
        int offset)
    {
        var movedGrids = CollectGridSet(grid);
        MoveGridSetToMap(movedGrids, targetMap.Owner, offset, targetMap.Comp1.Depth);

        return true;
    }

    /// <summary>
    /// Collects the grid and everything docked to it, dropping members that aren't on
    /// the same map (docked chains can span maps in degenerate cases).
    /// </summary>
    private HashSet<EntityUid> CollectGridSet(EntityUid grid)
    {
        var sourceMap = Transform(grid).MapUid;

        var movedGrids = new HashSet<EntityUid>();
        _shuttle.GetAllDockedShuttles(grid, movedGrids);
        movedGrids.Add(grid);
        movedGrids.RemoveWhere(uid => TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid) ||
            Transform(uid).MapUid != sourceMap);

        return movedGrids;
    }

    /// <summary>Every grid currently on a map (a transit map hosts exactly one set).</summary>
    private HashSet<EntityUid> CollectGridsOnMap(EntityUid mapUid)
    {
        var grids = new HashSet<EntityUid>();
        var children = Transform(mapUid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (_mapGridQuery.HasComp(child) && !TerminatingOrDeleted(child) && !EntityManager.IsQueuedForDeletion(child))
                grids.Add(child);
        }

        return grids;
    }

    public HashSet<EntityUid> CollectTransitSet(EntityUid grid) =>
        Transform(grid).MapUid is { } map && HasComp<CEZTransitMapComponent>(map)
            ? CollectGridsOnMap(map)
            : CollectGridSet(grid);

    /// <summary>
    /// Moves a set of grids to another map.
    /// </summary>
    private void MoveGridSetToMap(HashSet<EntityUid> movedGrids, EntityUid targetMap, int offset, int depth)
    {
        foreach (var gridUid in movedGrids)
        {
            var beforeEv = new CEZLevelBeforeMapMoveEvent(offset, depth);
            RaiseLocalEvent(gridUid, ref beforeEv);
        }

        foreach (var gridUid in movedGrids)
        {
            var xform = Transform(gridUid);
            var worldPos = _transform.GetWorldPosition(xform);
            var worldRot = _transform.GetWorldRotation(xform);

            // The map change wipes joints and can reset momentum, so save and restore it.
            var linVel = Vector2.Zero;
            var angVel = 0f;
            TryComp<PhysicsComponent>(gridUid, out var body);
            if (body != null)
            {
                linVel = body.LinearVelocity;
                angVel = body.AngularVelocity;
            }

            _transform.SetCoordinates(gridUid, xform, new EntityCoordinates(targetMap, worldPos), rotation: worldRot);

            if (body != null)
            {
                _physics.SetLinearVelocity(gridUid, linVel, body: body);
                _physics.SetAngularVelocity(gridUid, angVel, body: body);
            }
        }

        var ev = new CEZLevelMapMoveEvent(offset, depth);
        foreach (var gridUid in movedGrids)
        {
            _dockSystem.RedockDocks(gridUid);
            _console.RefreshShuttleConsoles(gridUid);

            RaiseLocalEvent(gridUid, ref ev);
            RaiseZMoveEventOnPassengers(gridUid, ref ev);
        }
    }

    /// <summary>
    /// Notifies everything riding the grid about the z-level change so cached values
    /// like <see cref="CEZPhysicsComponent.CurrentZLevel"/> stay correct.
    /// </summary>
    private void RaiseZMoveEventOnPassengers(EntityUid uid, ref CEZLevelMapMoveEvent ev)
    {
        var children = Transform(uid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (ZPhysicsQuery.HasComp(child))
                RaiseLocalEvent(child, ref ev);

            RaiseZMoveEventOnPassengers(child, ref ev);
        }
    }

    // === WARNING: A severe amount of this system was written with the assistance of LLMs. Expect bugs, inconsistent behaviour, and potentially stupid mistakes. ===
    // === While this system was manually reviewed and tested, I have no way of knowing entirely if it will fully hold up without live usage. ===

    /// <summary>
    /// Moves a grid (and its docked set) into a fresh transit map.
    /// </summary>
    public bool TryEnterTransit(Entity<MapGridComponent> grid, float? startProgress = null, bool preferUpperGap = false)
    {
        var xform = Transform(grid);

        if (xform.MapUid is not { } currentMap)
            return false;

        if (HasComp<CEZTransitMapComponent>(currentMap))
            return false; // Already in transit.

        if (!HasComp<CEZMapComponent>(currentMap))
            return false;

        var goDown = !preferUpperGap && TryMapDown(currentMap, out _);
        if (!(goDown ? TryMapDown(currentMap, out var neighbor) : TryMapUp(currentMap, out neighbor)) ||
            !TryGetLiveTransitAnchor(neighbor.Owner, out _))
            return false;

        var grids = CollectGridSet(grid);
        if (grids.Count == 0)
            return false;

        var transitMap = CreateTransitMap(goDown ? neighbor.Owner : currentMap, goDown ? currentMap : neighbor.Owner, grid);
        var progress = Math.Clamp(startProgress ?? (goDown ? 1f : 0f), 0f, 1f);
        MoveGridSetToMap(grids, transitMap, goDown ? -1 : 1, neighbor.Comp.Depth);
        foreach (var uid in grids)
        {
            var zPhys = EnsureComp<CEZPhysicsComponent>(uid);
            SetZPosition((uid, zPhys), progress);
            _pvsOverride.AddGlobalOverride(uid);
            _shuttle.Enable(uid);
        }

        return true;
    }

    /// <summary>
    /// Sets a transiting grid set's altitude in the z-network's depth coordinates.
    /// </summary>
    public bool SetTransitAltitude(Entity<MapGridComponent> grid, float altitude)
    {
        var xform = Transform(grid);

        if (xform.MapUid is not { } transitMapUid ||
            !TryComp<CEZTransitMapComponent>(transitMapUid, out var transit))
        {
            return false;
        }

        if (transit.LowerMap is not { } anchor ||
            !TryComp<CEZMapComponent>(anchor, out var anchorZ))
        {
            return false;
        }

        var progress = altitude - anchorZ.Depth;
        var span = GetTransitSpan(transit);

        while (progress > span)
        {
            if (transit.UpperMap is not { } upper)
            {
                progress = span;
                break;
            }

            // Terrain overhead is a solid ceiling from below: a ship rises through a hole in it
            // but never punches through the tiles themselves. Mirror of the descent rule (which
            // lands ON terrain, never past it). Clamp to the underside and stop.
            if (TransitBlockedByPlane(transitMapUid, upper))
            {
                progress = span;
                break;
            }

            if (!TryMapUp(upper, out var above))
            {
                // Top of the network: give on-demand generation a chance to extend it
                // upward before clamping.
                RaiseExpandEvent(upper, up: true);

                if (!TryMapUp(upper, out above))
                {
                    if (transit.PrimaryGrid is { } primary && !TerminatingOrDeleted(primary))
                    {
                        var ev = new CEZOpenSkyExitAttemptEvent(upper, GetTransitVerticalInput(transitMapUid) > 0f);
                        RaiseLocalEvent(primary, ref ev);
                    }

                    progress = span;
                    break;
                }
            }

            progress -= span;
            transitMapUid = HopTransitGap((transitMapUid, transit), upper, above.Owner, above.Comp.Depth, up: true);
            transit = Comp<CEZTransitMapComponent>(transitMapUid);
            span = GetTransitSpan(transit);
        }

        while (progress < 0f)
        {
            if (transit.LowerMap is not { } lower || TransitBlockedByPlane(transitMapUid, lower))
                return TryExitTransit(grid, up: false);

            if (!TryMapDown(lower, out var below))
            {
                // Once again, give procgen a chance.
                RaiseExpandEvent(lower, up: false);

                if (!TryMapDown(lower, out below))
                    return TryExitTransit(grid, up: false);
            }

            transitMapUid = HopTransitGap((transitMapUid, transit), below.Owner, lower, below.Comp.Depth, up: false);
            transit = Comp<CEZTransitMapComponent>(transitMapUid);
            span = GetTransitSpan(transit);
            progress += span;
        }

        // Hold a hair clear of a ceiling we're pinned under, so it keeps drawing overhead.
        // Applied here rather than inside the climb loop above because progress also creeps
        // into the top of the gap without ever exceeding 1f — clamping only on overshoot
        // would let the ship drift flush, then snap back down, once per overshoot.
        if (progress > span - CeilingClearance
            && transit.UpperMap is { } ceiling
            && TransitBlockedByPlane(transitMapUid, ceiling))
        {
            progress = span - CeilingClearance;
        }

        foreach (var uid in CollectGridsOnMap(transitMapUid))
            SetZPosition(uid, progress / span);

        return true;
    }

    private void CleanupOrphanedTransitMaps()
    {
        List<Entity<CEZTransitMapComponent>>? orphaned = null;
        var query = EntityQueryEnumerator<CEZTransitMapComponent>();
        while (query.MoveNext(out var uid, out var transit))
        {
            if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid))
                continue;

            if (transit.PrimaryGrid is { } primary &&
                !TerminatingOrDeleted(primary) &&
                !EntityManager.IsQueuedForDeletion(primary) &&
                Transform(primary).MapUid == uid)
            {
                continue;
            }

            (orphaned ??= new()).Add((uid, transit));
        }

        if (orphaned == null)
            return;

        foreach (var gap in orphaned)
        {
            var remaining = CollectGridsOnMap(gap);
            while (remaining.Count > 0)
            {
                var primary = remaining.First();
                var cluster = TakeDockedCluster(primary, remaining);
                var next = CreateTransitMap(gap.Comp.LowerMap, gap.Comp.UpperMap, primary);
                Comp<CEZTransitMapComponent>(next).AutoLand = gap.Comp.AutoLand;
                var depth = ZPhysicsQuery.TryComp(primary, out var zPhysics) ? zPhysics.CurrentZLevel : 0;
                MoveGridSetToMap(cluster, next, 0, depth);
            }

            _transitLastProgress.Remove(gap);
            QueueDel(gap);
        }

        QueueAllViewerUpdates();
    }

    private HashSet<EntityUid> TakeDockedCluster(EntityUid primary, HashSet<EntityUid> remaining)
    {
        var cluster = new HashSet<EntityUid> { primary };
        var pending = new Queue<EntityUid>();
        remaining.Remove(primary);
        pending.Enqueue(primary);

        while (pending.TryDequeue(out var grid))
        {
            foreach (var dock in _dockSystem.GetDocks(grid))
            {
                if (!TryComp<TransformComponent>(dock.Comp.DockedWith, out var otherDock) ||
                    otherDock.GridUid is not { } neighbor || !remaining.Remove(neighbor))
                {
                    continue;
                }

                cluster.Add(neighbor);
                pending.Enqueue(neighbor);
            }
        }

        return cluster;
    }

    /// <summary>
    /// Whether terrain on the plane blocks any grid in this transit map.
    /// </summary>
    private bool TransitBlockedByPlane(EntityUid transitMap, EntityUid plane)
    {
        foreach (var grid in CollectGridsOnMap(transitMap))
        {
            if (TryComp<MapGridComponent>(grid, out var gridComp)
                && HasGroundUnderFootprint((grid, gridComp), plane))
            {
                return true;
            }
        }

        return false;
    }

    private void RaiseExpandEvent(EntityUid edgeMap, bool up)
    {
        if (!TryGetMapNetwork(edgeMap, out var network))
            return;

        var ev = new CEZNetworkExpandRequestEvent(network, edgeMap, up);
        RaiseLocalEvent(network.Owner, ref ev);
    }

    /// <summary>
    /// Moves the docked set into the next gap and sweeps it through the crossed plane.
    /// </summary>
    private EntityUid HopTransitGap(Entity<CEZTransitMapComponent> old, EntityUid lower, EntityUid upper, int depth, bool up)
    {
        var next = CreateTransitMap(lower, upper, old.Comp.PrimaryGrid ?? EntityUid.Invalid);
        Comp<CEZTransitMapComponent>(next).AutoLand = old.Comp.AutoLand;
        var grids = CollectGridsOnMap(old);
        MoveGridSetToMap(grids, next, up ? 1 : -1, depth);

        foreach (var uid in grids)
            _shuttle.Smimsh(uid, crushMap: up ? lower : upper, explodeGrids: true);

        QueueDel(old);
        QueueAllViewerUpdates();
        return next;
    }

    /// <summary>
    /// Land whatever's on this transit map to the closest normal layer.
    /// </summary>
    public bool TryExitTransit(Entity<MapGridComponent> grid, bool? up = null)
    {
        var xform = Transform(grid);

        if (xform.MapUid is not { } transitMap)
            return false;

        if (!TryComp<CEZTransitMapComponent>(transitMap, out var transit))
            return false;

        var goingUp = !transit.AutoLand && (up ?? GetTransitProgress(transit) >= 0.5f);
        if (!TryGetLiveTransitAnchor(goingUp ? transit.UpperMap : transit.LowerMap, out var destination))
            return false;

        var grids = CollectGridsOnMap(transitMap);
        var depth = destination.Comp.Depth;
        foreach (var member in grids)
        {
            var from = GetAbsoluteAltitude(member);
            if (_shieldContainment.ClampVertical(member, transitMap, from, depth, false,
                    gridFootprint: true, grids) != depth)
                return false;
        }

        TryComp<CEZGridFallerComponent>(grid, out var faller);
        var impact = goingUp ? 0f : faller?.Velocity ?? 0f;
        MoveGridSetToMap(grids, destination, goingUp ? 1 : -1, depth);
        foreach (var uid in grids)
        {
            ResetGridTransitState(uid);
            if (TryComp<CEZGridFallerComponent>(uid, out var memberFaller))
                memberFaller.Velocity = 0f;

            // Landing on a grid causes an explosion. Don't do that.
            _shuttle.Smimsh(uid, explodeGrids: true, ignoredGrids: grids);
        }

        if (faller != null)
            HandleGridTouchdown(destination, grids, impact, faller.GridCrashVelocity);

        QueueDel(transitMap);
        QueueAllViewerUpdates();
        return true;
    }

    private void ResetGridTransitState(EntityUid grid)
    {
        if (ZPhysicsQuery.TryComp(grid, out var zPhys))
        {
            SetZPosition((grid, zPhys), 0f);
            SetZVelocity((grid, zPhys), 0f);
        }

        _pvsOverride.RemoveGlobalOverride(grid);
    }

    public bool TryInsertIntoOpenSky(Entity<MapGridComponent> grid,
        Entity<CEZMapNetworkComponent> network,
        Vector2 target)
    {
        if (Transform(grid).MapUid is not { } sourceMap ||
            HasComp<CEZMapComponent>(sourceMap) ||
            HasComp<CEZTransitMapComponent>(sourceMap) ||
            network.Comp.SortedZLevels.Count == 0)
        {
            return false;
        }

        EntityUid? topMap = null;
        for (var i = network.Comp.SortedZLevels.Count - 1; i >= 0; i--)
        {
            var candidate = network.Comp.SortedZLevels[i];
            if (candidate.IsValid() && TryComp<CEZMapComponent>(candidate, out _))
            {
                topMap = candidate;
                break;
            }
        }

        if (topMap is not { } top || !TryComp<CEZMapComponent>(top, out var topZ))
            return false;

        var movedGrids = CollectGridSet(grid);
        if (movedGrids.Count == 0)
            return false;

        RecenterGridSet(movedGrids, grid, target);
        var transitMap = CreateTransitMap(top, null, grid);
        Comp<CEZTransitMapComponent>(transitMap).AutoLand = true;
        MoveGridSetToMap(movedGrids, transitMap, 0, topZ.Depth);

        foreach (var gridUid in movedGrids)
        {
            var zPhys = EnsureComp<CEZPhysicsComponent>(gridUid);
            SetZPosition((gridUid, zPhys), 1f);
            SetZVelocity((gridUid, zPhys), 0f);
            if (TryComp<CEZGridFallerComponent>(gridUid, out var faller))
                faller.Velocity = 0f;

            _pvsOverride.AddGlobalOverride(gridUid);
            _shuttle.Enable(gridUid);
        }

        return true;
    }

    public bool TryCreateDetachedTransit(Entity<MapGridComponent> grid, out EntityUid detachedMap)
    {
        detachedMap = default;
        if (Transform(grid).MapUid is not { } sourceMap)
            return false;

        var movedGrids = CollectTransitSet(grid);

        if (movedGrids.Count == 0)
            return false;

        detachedMap = _map.CreateMap(out _);
        var light = EnsureComp<MapLightComponent>(detachedMap);
        if (TryComp<MapLightComponent>(sourceMap, out var sourceLight))
            light.AmbientLightColor = sourceLight.AmbientLightColor;
        Dirty(detachedMap, light);

        MoveDetachedGridSet(movedGrids, detachedMap);
        foreach (var gridUid in movedGrids)
            _pvsOverride.AddGlobalOverride(gridUid);

        if (HasComp<CEZTransitMapComponent>(sourceMap))
            QueueDel(sourceMap);

        QueueAllViewerUpdates();
        return true;
    }

    public bool TryMoveDetachedTransit(Entity<MapGridComponent> grid, EntityUid targetMap, Vector2 target)
    {
        if (Transform(grid).MapUid is not { } sourceMap ||
            TerminatingOrDeleted(targetMap) ||
            HasComp<CEZTransitMapComponent>(targetMap))
        {
            return false;
        }

        var movedGrids = CollectGridsOnMap(sourceMap);
        if (movedGrids.Count == 0)
            return false;

        RecenterGridSet(movedGrids, grid, target);
        if (TryComp<CEZMapComponent>(targetMap, out var zMap))
            MoveGridSetToMap(movedGrids, targetMap, 0, zMap.Depth);
        else
            MoveDetachedGridSet(movedGrids, targetMap);

        foreach (var gridUid in movedGrids)
            _pvsOverride.RemoveGlobalOverride(gridUid);

        QueueAllViewerUpdates();
        return true;
    }

    public bool TryRecenterDetachedTransit(Entity<MapGridComponent> grid, Vector2 target)
    {
        if (Transform(grid).MapUid is not { } sourceMap)
            return false;

        var movedGrids = CollectGridsOnMap(sourceMap);
        if (movedGrids.Count == 0)
            return false;

        RecenterGridSet(movedGrids, grid, target);
        return true;
    }

    private void MoveDetachedGridSet(HashSet<EntityUid> grids, EntityUid targetMap)
    {
        foreach (var gridUid in grids)
        {
            if (TerminatingOrDeleted(gridUid))
                continue;

            var xform = Transform(gridUid);
            var worldPos = _transform.GetWorldPosition(xform);
            var worldRot = _transform.GetWorldRotation(xform);
            TryComp<PhysicsComponent>(gridUid, out var body);
            var linear = body?.LinearVelocity ?? Vector2.Zero;
            var angular = body?.AngularVelocity ?? 0f;

            _transform.SetCoordinates(gridUid, xform, new EntityCoordinates(targetMap, worldPos), rotation: worldRot);

            if (body != null)
            {
                _physics.SetLinearVelocity(gridUid, linear, body: body);
                _physics.SetAngularVelocity(gridUid, angular, body: body);
            }
        }

        foreach (var gridUid in grids)
        {
            _dockSystem.RedockDocks(gridUid);
            _console.RefreshShuttleConsoles(gridUid);
        }
    }

    public bool TryExitOpenSky(Entity<MapGridComponent> grid, EntityUid targetMap, Vector2 target)
    {
        if (Transform(grid).MapUid is not { } transitMap ||
            !TryComp<CEZTransitMapComponent>(transitMap, out _) ||
            TerminatingOrDeleted(targetMap) ||
            HasComp<CEZMapComponent>(targetMap) ||
            HasComp<CEZTransitMapComponent>(targetMap))
        {
            return false;
        }

        var movedGrids = CollectGridsOnMap(transitMap);
        if (movedGrids.Count == 0)
            return false;

        RecenterGridSet(movedGrids, grid, target);
        MoveGridSetToMap(movedGrids, targetMap, 0, 0);

        foreach (var gridUid in movedGrids)
            _pvsOverride.RemoveGlobalOverride(gridUid);

        QueueDel(transitMap);
        QueueAllViewerUpdates();
        return true;
    }

    private void RecenterGridSet(HashSet<EntityUid> grids, EntityUid primary, Vector2 target)
    {
        var delta = target - _transform.GetWorldPosition(primary);
        foreach (var gridUid in grids)
        {
            if (!TerminatingOrDeleted(gridUid))
                _transform.SetWorldPosition(gridUid, _transform.GetWorldPosition(gridUid) + delta);
        }
    }

    private EntityUid CreateTransitMap(EntityUid? lowerMap, EntityUid? upperMap, EntityUid primaryGrid)
    {
        var mapUid = _map.CreateMap(out _);

        var transit = AddComp<CEZTransitMapComponent>(mapUid);
        transit.LowerMap = lowerMap;
        transit.UpperMap = upperMap;
        transit.PrimaryGrid = primaryGrid;
        Dirty(mapUid, transit);

        // Same environment as the network's z-levels (eg. atmosphere); this should really become configurable with real planets.
        if ((lowerMap ?? upperMap) is { } anchor &&
            TryGetMapNetwork(anchor, out var network) && network.Comp.Components.Count > 0)
            EntityManager.AddComponents(mapUid, network.Comp.Components, removeExisting: false);

        // Copy the lighting from the upper level (or lower if there isn't any above) so you can see.
        var light = EnsureComp<MapLightComponent>(mapUid);
        if (upperMap is { } upper && TryComp<MapLightComponent>(upper, out var upperLight))
            light.AmbientLightColor = upperLight.AmbientLightColor;
        else if (TryComp<MapLightComponent>(lowerMap, out var lowerLight))
            light.AmbientLightColor = lowerLight.AmbientLightColor;
        Dirty(mapUid, light);

        _meta.SetEntityName(mapUid, TryComp<MetaDataComponent>(lowerMap, out var lowerMeta)
            ? $"Z-Transit above {lowerMeta.EntityName}"
            : "Z-Transit");

        QueueAllViewerUpdates();

        return mapUid;
    }
}

[ByRefEvent]
public record struct CEZNetworkExpandRequestEvent(
    Entity<CEZMapNetworkComponent> Network,
    EntityUid EdgeMap,
    bool Up);

[ByRefEvent]
public record struct CEZOpenSkyExitAttemptEvent(EntityUid TopMap, bool Pushing)
{
    public bool Handled;
}
