/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._CE.ZLevels.Core.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Gravity;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Gravity;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private GravitySystem _grav = default!;

    [Dependency] private EntityQuery<CEZMapComponent> _zMapQuery = default!;
    [Dependency] private EntityQuery<CEZGroundLayerComponent> _zGroundQuery = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physQuery = default!;

    private readonly List<Entity<MapGridComponent>> _gravityQueue = new();

    /// <summary>
    /// pzn: pooled MaxHandledMass of active gravgens per grid, rebuilt in one pass
    /// each gravity sweep. PositiveInfinity = at least one unrated (unlimited)
    /// generator. Falling grids read this every frame, so it must stay a lookup.
    /// </summary>
    private readonly Dictionary<EntityUid, float> _gravgenCapacity = new();
    private readonly TimeSpan _gravityCheckTimer = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextGravityCheckTime;

    /// <summary>
    /// Grid gravity: unsupported grids on z-levels start falling (into transit), and
    /// grids in transit accelerate downward until a gravity generator or the ground
    /// says otherwise.
    /// </summary>
    private void UpdateGridGravity(float frameTime)
    {
        // Pilot vertical flight: read the consoles, then let parked ships spool up.
        CollectPilotVerticalInputs();
        UpdateTakeoffSpool();

        // Throttle checking for grid gravity so the server doesn't set itself on fire.
        if (_timing.CurTime >= _nextGravityCheckTime)
        {
            _nextGravityCheckTime = _timing.CurTime + _gravityCheckTimer;

            // Collect first: entering/hopping transit adds components mid-query otherwise.
            _gravityQueue.Clear();

            // What actually holds a grid aloft is a working gravgen, the same thing
            // GravitySystem.RefreshGravity scans for. Precompute pooled generator
            // capacity per grid so the gate below stays O(grids + gravgens).
            _gravgenCapacity.Clear();
            var gravgenQuery = EntityQueryEnumerator<GravityGeneratorComponent, TransformComponent>();
            while (gravgenQuery.MoveNext(out _, out var gravgen, out var gravgenXform))
            {
                if (!gravgen.GravityActive || !gravgenXform.ParentUid.IsValid())
                    continue;

                // Unrated (<= 0) = unlimited; infinity absorbs any finite additions.
                var rated = gravgen.MaxHandledMass <= 0f ? float.PositiveInfinity : gravgen.MaxHandledMass;
                _gravgenCapacity[gravgenXform.ParentUid] =
                    _gravgenCapacity.GetValueOrDefault(gravgenXform.ParentUid) + rated;
            }

            // Mapping anchors are unlimited lift: a grid hosting one holds its whole network
            // aloft regardless of mass (see CEZMappingAnchorComponent).
            var anchorQuery = EntityQueryEnumerator<CEZMappingAnchorGridComponent>();
            while (anchorQuery.MoveNext(out var anchorGrid, out _))
            {
                _gravgenCapacity[anchorGrid] = float.PositiveInfinity;
            }

            var levelQuery = EntityQueryEnumerator<CEZGridFallerComponent, MapGridComponent>();
            while (levelQuery.MoveNext(out var uid, out var faller, out var grid))
            {
                if (_timing.CurTime < faller.GravityTime)
                    continue;

                var xform = Transform(uid);

                if (xform.MapUid is not { } mapUid || !_zMapQuery.HasComp(mapUid))
                    continue;

                // You can't fall out of the ground floor.
                if (_zGroundQuery.HasComp(mapUid))
                    continue;

                // Parked/anchored ships hold position.
                if (_physQuery.TryComp(uid, out var body) && body.BodyType == BodyType.Static)
                    continue;

                // NOTE: Can't use IsWeightless() here - Monolith's rewrite requires a
                // GravityAffectedComponent on the entity, which grids never have, so it
                // always returns false for grids. Also can't use
                // EntityGridOrMapHaveGravity(): it falls back to the parent *map*, and
                // ground-layer maps carry inherent gravity (so mobs on the ground don't
                // float), which would mean no grid on them ever falls.
                // Only a working gravgen on the grid itself keeps it aloft.
                if (GridHasActiveGravgen(uid))
                    continue;

                if (HasGroundUnderFootprint((uid, grid), mapUid))
                    continue;

                // A z-network falls as one rigid stack: it stays up when its pooled
                // gravgen capacity covers the whole network's mass, or a member rests
                // on ground. Unsupported networks enter transit together via the
                // convoy path in TryEnterTransit.
                if (TryGetGridNetwork(uid, out var supportNet) && NetworkHasSupport(supportNet))
                    continue;

                _gravityQueue.Add((uid, grid));
            }

            foreach (var grid in _gravityQueue)
            {
                if (TryComp<CEZGridFallerComponent>(grid, out var faller))
                    faller.Velocity = 0f;

                TryEnterTransit(grid); // Plummet.
            }
        }

        // Clear out the queue.
        _gravityQueue.Clear();

        var transitQuery = EntityQueryEnumerator<CEZTransitMapComponent>();
        while (transitQuery.MoveNext(out var transitUid, out var transit))
        {
            if (TerminatingOrDeleted(transitUid) || EntityManager.IsQueuedForDeletion(transitUid))
                continue;

            if (transit.PrimaryGrid is not { } primary ||
                TerminatingOrDeleted(primary) ||
                !TryComp<MapGridComponent>(primary, out var primaryGrid))
            {
                continue;
            }

            // Convoy follower layers don't integrate — they mirror the lead layer's
            // velocity inside IntegrateFallingGrid.
            if ((transit.TransitAbove != null || transit.TransitBelow != null) && !transit.ConvoyLead)
                continue;

            _gravityQueue.Add((primary, primaryGrid));
        }

        foreach (var grid in _gravityQueue)
        {
            IntegrateFallingGrid(grid, frameTime);
        }

        CheckTransitCollisions();
    }

    /// <summary>
    /// pzn: transit sets passing through each other explode. Every transit set rides
    /// its own map, so physics never sees the overlap — sweep pairs of transit maps
    /// sharing the same gap and, when their vertical bands and footprints intersect,
    /// let each set flatten the other (both ways, so whichever side is "in transit"
    /// makes no difference).
    /// </summary>
    private readonly List<(EntityUid Map, CEZTransitMapComponent Transit, EntityUid Primary, float Progress)> _transitCollisionScan = new();

    /// <summary>
    /// Progress of every transit map on the previous gravity tick, used for the
    /// pairwise crossing test in <see cref="CheckTransitCollisions"/>.
    /// </summary>
    private readonly Dictionary<EntityUid, float> _transitLastProgress = new();

    private void CheckTransitCollisions()
    {
        _transitCollisionScan.Clear();

        var query = EntityQueryEnumerator<CEZTransitMapComponent>();
        while (query.MoveNext(out var uid, out var transit))
        {
            if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid))
                continue;

            if (transit.PrimaryGrid is not { } primary ||
                TerminatingOrDeleted(primary) ||
                !ZPhysicsQuery.TryComp(primary, out var zPhys))
            {
                continue;
            }

            _transitCollisionScan.Add((uid, transit, primary, zPhys.LocalPosition));
        }

        for (var i = 0; i < _transitCollisionScan.Count; i++)
        {
            var a = _transitCollisionScan[i];

            for (var j = i + 1; j < _transitCollisionScan.Count; j++)
            {
                var b = _transitCollisionScan[j];

                // Only sets sharing the same gap can meet.
                if (a.Transit.LowerMap != b.Transit.LowerMap || a.Transit.UpperMap != b.Transit.UpperMap)
                    continue;

                // Crossing test: collide only when the pair's vertical order flips
                // (or they meet exactly) between ticks. Fires once at the crossing
                // regardless of speed — a distance band would re-trigger every tick
                // for slow sets and tunnel straight through for fast ones.
                if (!_transitLastProgress.TryGetValue(a.Map, out var prevA) ||
                    !_transitLastProgress.TryGetValue(b.Map, out var prevB))
                {
                    continue; // first tick tracked for this pair
                }

                var prevDelta = prevA - prevB;
                var curDelta = a.Progress - b.Progress;

                // Same ordering on both ticks — they never met.
                if (prevDelta * curDelta > 0f)
                    continue;

                // Cheap broadphase before Smimsh: all z-maps share one coordinate
                // space, so world AABBs compare directly across the two transit maps.
                if (!TryGetGridSetAabb(a.Primary, out var setA, out var aabbA) ||
                    !TryGetGridSetAabb(b.Primary, out var setB, out var aabbB) ||
                    !aabbA.Intersects(aabbB))
                {
                    continue;
                }

                // Mutual crush: each set explodes whatever of the other sits in its
                // footprint. CrushGrid deletes the victim, so this can't re-fire.
                foreach (var gridUid in setA)
                    _shuttle.Smimsh(gridUid, crushMap: b.Map, explodeGrids: true, ignoredGrids: setA);

                foreach (var gridUid in setB)
                {
                    if (TerminatingOrDeleted(gridUid) || EntityManager.IsQueuedForDeletion(gridUid))
                        continue;

                    _shuttle.Smimsh(gridUid, crushMap: a.Map, explodeGrids: true, ignoredGrids: setB);
                }
            }
        }

        // Refresh tracking; clear-and-refill also prunes maps whose transit ended.
        _transitLastProgress.Clear();
        foreach (var entry in _transitCollisionScan)
        {
            _transitLastProgress[entry.Map] = entry.Progress;
        }
    }

    /// <summary>
    /// Collects a transit set and the union of its members' world AABBs.
    /// </summary>
    private bool TryGetGridSetAabb(EntityUid primary, out HashSet<EntityUid> set, out Box2 aabb)
    {
        set = CollectGridSet(primary);
        aabb = default;

        var first = true;
        foreach (var gridUid in set)
        {
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            var worldAabb = _transform.GetWorldMatrix(gridUid).TransformBox(grid.LocalAABB);
            aabb = first ? worldAabb : aabb.Union(worldAabb);
            first = false;
        }

        return !first;
    }

    /// <summary>
    /// Accelerates a velocity toward a terminal speed on a smooth curve rather than
    /// a hard clamp: full acceleration at rest, tapering to zero as the speed nears
    /// <paramref name="terminalSpeed"/>, and none at all beyond it — so an
    /// over-terminal launch boost coasts instead of being yanked back to the cap.
    /// <paramref name="signedAccel"/>'s sign is the direction (positive = down, to
    /// match <see cref="CEZGridFallerComponent.Velocity"/>).
    /// </summary>
    private static float ApproachTerminal(float velocity, float signedAccel, float terminalSpeed, float frameTime)
    {
        if (terminalSpeed <= 0f || signedAccel == 0f)
            return velocity;

        // Current speed in the direction we're pushing (0 if already moving the other way).
        var speedInDir = signedAccel > 0f ? MathF.Max(0f, velocity) : MathF.Max(0f, -velocity);
        var taper = Math.Clamp(1f - speedInDir / terminalSpeed, 0f, 1f);
        return velocity + signedAccel * taper * frameTime;
    }

    /// <summary>
    /// Moves a value toward a target by at most <paramref name="maxDelta"/>. Used to
    /// change fall speed at a bounded rate so it never snaps to a new value in a
    /// single tick.
    /// </summary>
    private static float MoveTowards(float current, float target, float maxDelta)
    {
        var diff = target - current;
        return MathF.Abs(diff) <= maxDelta ? target : current + MathF.Sign(diff) * maxDelta;
    }

    private void IntegrateFallingGrid(Entity<MapGridComponent> grid, float frameTime)
    {
        if (!TryComp<CEZGridFallerComponent>(grid, out var faller) ||
            !ZPhysicsQuery.TryComp(grid, out var zPhys))
        {
            return;
        }

        var xform = Transform(grid);
        if (!TryComp<CEZTransitMapComponent>(xform.MapUid, out var transit) ||
            transit.LowerMap is not { } lowerMap ||
            !TryComp<CEZMapComponent>(lowerMap, out var lowerZ))
        {
            return;
        }

        var progress = zPhys.LocalPosition;

        // Convoy-aware bounds: the stack lands on whatever is under its BOTTOM layer
        // and settles up against whatever is above its TOP layer; any member's
        // gravgen keeps the whole thing aloft (the connectors carry the load).
        var convoy = GetConvoyMaps(xform.MapUid!.Value);
        var groundMapBelow = Comp<CEZTransitMapComponent>(convoy[0]).LowerMap ?? lowerMap;
        var topUpperMap = Comp<CEZTransitMapComponent>(convoy[^1]).UpperMap;

        var transitSet = CollectTransitSet(grid);

        // The whole rigid set hovers only if its generators' pooled capacity holds the
        // set's pooled mass — not if any single member's generator holds only itself.
        var hasGravgen = HasPooledGravgenSupport(transitSet);

        if (!hasGravgen)
        {
            // AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
            if (_timing.CurTime < faller.GravityTime)
                return;

            faller.Velocity = ApproachTerminal(faller.Velocity, faller.GridGravity, faller.GridTerminalVelocity, frameTime);
        }
        else
        {
            var input = GetConvoyVerticalInput(convoy);
            var accel = GetVerticalThrustAccel(grid);
            var damp = Math.Max(accel, HoverDampAccel);

            if (input != 0f && accel > 0f)
            {
                // Flight.
                faller.Velocity = ApproachTerminal(faller.Velocity, -input * accel, MaxPilotVerticalSpeed, frameTime);
            }
            else
            {
                // No pilot input: ease toward a target speed at a bounded rate so the
                // velocity never snaps. Mid-gap the target is zero (hover); within a
                // settle zone it's a gentle drift onto the nearer plane, scaled down
                // by the remaining distance so touchdown is soft.
                var target = 0f;

                if (progress <= SettleZone)
                {
                    if (progress <= TouchdownProgress && MathF.Abs(faller.Velocity) <= ExitTransitMaxSpeed)
                    {
                        faller.Velocity = 0f;
                        TryExitTransit(grid);
                        return;
                    }

                    target = MathF.Max(TouchdownSpeed, progress * ApproachGain);
                }
                else if (progress >= 1f - SettleZone && topUpperMap != null)
                {
                    if (progress >= 1f - TouchdownProgress && MathF.Abs(faller.Velocity) <= ExitTransitMaxSpeed)
                    {
                        faller.Velocity = 0f;
                        TryExitTransit(grid);
                        return;
                    }

                    target = -MathF.Max(TouchdownSpeed, (1f - progress) * ApproachGain);
                }

                faller.Velocity = MoveTowards(faller.Velocity, target, damp * frameTime);
            }

            // Even under power you don't crater onto a ground layer: ease the descent
            // speed down to a distance-scaled cap. A non-ground plane can still be
            // punched through with the key held.
            if (faller.Velocity > 0f && HasComp<CEZGroundLayerComponent>(groundMapBelow))
            {
                var cap = MathF.Max(TouchdownSpeed, progress * ApproachGain);
                if (faller.Velocity > cap)
                    faller.Velocity = MoveTowards(faller.Velocity, cap, damp * frameTime);
            }
        }

        // Mirror the fall speed onto the networked z-physics velocity (which uses the
        // opposite sign: positive = up) so consoles can read it. Whole transit set,
        // since a console may sit on a docked companion or another network layer.
        // Follower fallers also track the lead's velocity so a mid-transit split
        // leaves them falling at a real speed instead of resetting to zero.
        foreach (var member in transitSet)
        {
            SetZVelocity(member, -faller.Velocity);

            if (member != grid.Owner && TryComp<CEZGridFallerComponent>(member, out var memberFaller))
                memberFaller.Velocity = faller.Velocity;
        }

        var altitude = lowerZ.Depth + progress - faller.Velocity * frameTime;
        if (!SetTransitAltitude(grid, altitude))
            return;

        // Still airborne?
        if (HasComp<CEZTransitMapComponent>(Transform(grid).MapUid))
            return;

        // Touched down (SetTransitAltitude landed us below the network's bottom).
        var impact = faller.Velocity;
        faller.Velocity = 0f;

        if (impact < faller.GridCrashVelocity || !HasComp<CEZGroundLayerComponent>(Transform(grid).MapUid))
            return;

        var crashSet = CollectGridSet(grid);
        if (TryGetGridNetwork(grid, out var landedNetwork))
        {
            foreach (var member in landedNetwork.Comp.Grids)
                crashSet.UnionWith(CollectGridSet(member));
        }

        foreach (var landedUid in crashSet)
        {
            if (TryComp<MapGridComponent>(landedUid, out var landedGrid) && TryComp<CEZGridFallerComponent>(landedUid, out var landedFaller))
                CrashGrid((landedUid, landedGrid, landedFaller));
        }
    }

    /// <summary>
    /// A hard ground-layer touchdown: a small explosion on every hull tile plus one
    /// central blast scaled by hull size.
    /// </summary>
    private void CrashGrid(Entity<MapGridComponent, CEZGridFallerComponent> ent)
    {
        var tileCount = 0;
        var tiles = _map.GetAllTilesEnumerator(ent, ent.Comp1);
        while (tiles.MoveNext(out var tileRef))
        {
            tileCount++;
            var coords = _map.GridTileToLocal(ent, ent.Comp1, tileRef.Value.GridIndices);
            _explosion.QueueExplosion(coords,
                ExplosionSystem.DefaultExplosionPrototypeId,
                ent.Comp2.CrashTileIntensity,
                ent.Comp2.CrashTileSlope,
                ent.Comp2.CrashTileMaxIntensity,
                cause: ent,
                addLog: false);
        }

        if (tileCount == 0)
            return;

        _explosion.QueueExplosion(ent.Owner,
            ExplosionSystem.DefaultExplosionPrototypeId,
            ent.Comp2.CrashIntensityPerTile * tileCount,
            ent.Comp2.CrashCenterSlope,
            ent.Comp2.CrashCenterMaxIntensity);
    }

    /// <summary>
    /// Whether any solid tile of the map lies under the grid's footprint.
    /// </summary>
    /// <summary>
    /// Same check GravitySystem effectively does when refreshing grid gravity —
    /// is there a gravgen parented to this grid that's actually producing gravity? —
    /// plus pzn mass limits: every active gravgen on the grid pools its
    /// <see cref="GravityGeneratorComponent.MaxHandledMass"/>, and if the grid's
    /// fixture mass (≈ tile count) exceeds the pooled capacity, the generators are
    /// overloaded and can't hold the grid aloft.
    /// </summary>
    private bool GridHasActiveGravgen(EntityUid grid)
    {
        // Reads the pooled capacity rebuilt each gravity sweep, so per-frame falling
        // integration stays O(1). At worst the answer is one sweep (0.5s) stale —
        // the same cadence the fall gate has always run on.
        if (!_gravgenCapacity.TryGetValue(grid, out var capacity))
            return false;

        if (float.IsPositiveInfinity(capacity))
            return true;

        var mass = _physQuery.TryComp(grid, out var body) ? body.FixturesMass : 0f;
        return mass <= capacity;
    }

    /// <summary>
    /// Whether the pooled rated capacity of every active gravity generator across a
    /// set of rigidly-joined grids (a z-network, or a docked set) can hold the set's
    /// pooled mass. Because the grids move as one body their generators share the
    /// whole load, so capacity and mass are summed across the set rather than each
    /// grid being weighed against only its own generators. An unrated generator
    /// anywhere in the set lifts any finite load.
    /// </summary>
    private bool HasPooledGravgenSupport(IEnumerable<EntityUid> grids)
    {
        var capacity = 0f;
        var mass = 0f;

        foreach (var grid in grids)
        {
            if (_gravgenCapacity.TryGetValue(grid, out var gridCapacity))
            {
                if (float.IsPositiveInfinity(gridCapacity))
                    return true;

                capacity += gridCapacity;
            }

            if (_physQuery.TryComp(grid, out var body))
                mass += body.FixturesMass;
        }

        // capacity > 0 means at least one active generator exists; a set with no lift
        // hardware is never "supported" even when it happens to be massless.
        return capacity > 0f && mass <= capacity;
    }

    /// <summary>
    /// A z-network falls as one rigid stack: it is held up when its generators' pooled
    /// capacity covers the whole network's mass, or when any single member is resting
    /// on ground under its footprint (the connectors carry the rest).
    /// </summary>
    private bool NetworkHasSupport(Entity<CEZGridNetworkComponent> network)
    {
        if (HasPooledGravgenSupport(network.Comp.Grids))
            return true;

        foreach (var member in network.Comp.Grids)
        {
            if (Transform(member).MapUid is { } memberMap
                && TryComp<MapGridComponent>(member, out var memberGrid)
                && HasGroundUnderFootprint((member, memberGrid), memberMap))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// pzn: on-demand load readout for examine/UI: the pooled physics mass and the
    /// pooled rated capacity of every *active* gravgen holding the grid up, same rules
    /// as the gravity sweep (negative = unlimited, 0 = no lift hardware). When the grid
    /// belongs to a z-grid network the whole network is pooled — matching the pooled
    /// support decision in <see cref="NetworkHasSupport"/> — so the readout reflects
    /// whether the rigid stack actually stays aloft, not just this one grid. Unlike
    /// <see cref="GridHasActiveGravgen"/> this recomputes instead of reading the
    /// throttled cache, so it's never stale.
    /// </summary>
    public bool TryGetGravgenLoad(EntityUid gridUid, out float gridMass, out float capacity)
    {
        gridMass = 0f;
        capacity = 0f;

        if (!HasComp<MapGridComponent>(gridUid))
            return false;

        // A z-grid network shares its generators' lift across every member, so the
        // load is pooled over the whole network; a lone grid answers for itself.
        var networkGrids = TryGetGridNetwork(gridUid, out var network) ? network.Comp.Grids : null;

        var query = EntityQueryEnumerator<GravityGeneratorComponent, TransformComponent>();
        while (query.MoveNext(out _, out var gravgen, out var xform))
        {
            if (!gravgen.GravityActive)
                continue;

            var onSet = networkGrids != null
                ? networkGrids.Contains(xform.ParentUid)
                : xform.ParentUid == gridUid;
            if (!onSet)
                continue;

            // pzn: negative rating = infinite lift; 0 = pure gravity, no lift hardware at all.
            capacity += gravgen.MaxHandledMass < 0f ? float.PositiveInfinity : gravgen.MaxHandledMass;
        }

        if (networkGrids != null)
        {
            foreach (var member in networkGrids)
            {
                if (_physQuery.TryComp(member, out var memberBody))
                    gridMass += memberBody.FixturesMass;
            }
        }
        else if (_physQuery.TryComp(gridUid, out var body))
        {
            gridMass = body.FixturesMass;
        }

        return true;
    }

    private bool HasGroundUnderFootprint(Entity<MapGridComponent> grid, EntityUid mapUid)
    {
        if (!TryComp<MapGridComponent>(mapUid, out var mapGrid))
            return false;

        var worldAabb = _transform.GetWorldMatrix(grid).TransformBox(grid.Comp.LocalAABB);
        var tiles = _map.GetTilesEnumerator(mapUid, mapGrid, worldAabb);
        return tiles.MoveNext(out _);
    }
}
