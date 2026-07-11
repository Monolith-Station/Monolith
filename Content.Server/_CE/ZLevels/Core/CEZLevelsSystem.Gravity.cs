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

    /// <summary>
    /// pzn: per-sweep memo of each grid's rigid-set support verdict. Every grid in one rigid
    /// body shares the same verdict, so the first grid of a body computes the flood + pooled
    /// support once and caches it for every member — the other members then skip the whole
    /// recomputation. Keeps the fall gate O(grids) instead of O(grids × body size). Cleared
    /// each sweep alongside <see cref="_gravgenCapacity"/>.
    /// </summary>
    private readonly Dictionary<EntityUid, bool> _rigidSupportCache = new();
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
            _rigidSupportCache.Clear();
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
                // Only a working gravgen keeps a set aloft.
                //
                // A grid is held up when its whole RIGID SET — its z-network members plus
                // everything docked to any of them — pools enough gravgen lift to carry the
                // set's pooled mass, or any member rests on ground. Connectors and docking
                // ports both bind grids into one falling body, so both pool their generators
                // AND their weight: a docked tug's gravgen can hold the set up, and its mass
                // also loads the set down. Unsupported sets enter transit together (the docked
                // partners ride along via CollectGridSet inside TryEnterTransit).
                if (RigidSetHasSupport(uid))
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
    /// The full rigid body a grid falls (or is held) as one with: the transitive closure over
    /// both welds — docking ports and z-network connectors — so two stacks docked together fold
    /// into a single set, no matter how many network-then-dock-then-network hops apart. Support
    /// is pooled across the whole union. A lone, undocked, networkless grid yields just itself.
    /// </summary>
    private HashSet<EntityUid> CollectRigidSet(EntityUid uid)
    {
        // Transitive flood over BOTH welds: docking ports and network connectors. One level
        // isn't enough — a docked partner can itself be a member of another z-network whose own
        // members are docked to yet more, and so on. Following only the starting grid's network
        // (and its members' docks) would miss the far side of a stack-to-stack dock, and worse,
        // give a different set depending on which grid you started from — so the same rigid body
        // could be judged supported from one end and falling from the other. The flood closes
        // over the whole connected body, so every grid in it yields the identical set.
        //
        // `set` guards enqueueing: each grid enters the queue only the first time it's seen, so
        // the flood always terminates and docking/network cycles are safe. `dockWalked` and
        // `netWalked` additionally guard the EXPENSIVE expansions: a docked component (returned
        // whole by GetAllDockedShuttles) and a network are each walked exactly once, instead of
        // re-walking the same cluster once per member.
        var set = new HashSet<EntityUid>();
        var queue = new Queue<EntityUid>();
        var dockWalked = new HashSet<EntityUid>();
        var netWalked = new HashSet<EntityUid>();

        void Add(EntityUid g)
        {
            if (set.Add(g))
                queue.Enqueue(g);
        }

        Add(uid);

        while (queue.Count > 0)
        {
            var g = queue.Dequeue();

            // Docking: GetAllDockedShuttles hands back g's whole docked component at once, and
            // every grid in it shares that same component — so walk it once, marking the lot.
            if (dockWalked.Add(g))
            {
                foreach (var docked in CollectGridSet(g))
                {
                    dockWalked.Add(docked);
                    Add(docked);
                }
            }

            // Connectors weld g to its z-network members; each may open onto a new docked
            // component, which the loop then walks. Expand each network only once.
            if (TryGetGridNetwork(g, out var network) && netWalked.Add(network.Owner))
            {
                foreach (var member in network.Comp.Grids)
                    Add(member);
            }
        }

        return set;
    }

    /// <summary>
    /// Whether a grid's whole rigid set is held aloft: its generators' pooled capacity covers
    /// the set's pooled mass (so a docked tug or a networked gravgen carries the rest, and every
    /// member's weight counts against that lift), or any member rests on ground under its
    /// footprint. Subsumes the old per-grid gravgen check and network-only pooling — for a lone
    /// grid the set is just itself, giving the identical result.
    ///
    /// Memoized per sweep: every grid in one rigid body shares this verdict, so the first grid
    /// floods + pools once and caches the answer for every member; the rest hit the cache. The
    /// fall gate calls this once per grid, so without the cache an N-grid body would recompute
    /// the whole flood N times (O(N²)); with it, once (O(N)).
    /// </summary>
    private bool RigidSetHasSupport(EntityUid uid)
    {
        if (_rigidSupportCache.TryGetValue(uid, out var cached))
            return cached;

        var set = CollectRigidSet(uid);
        var supported = SetHasSupport(set);

        foreach (var member in set)
            _rigidSupportCache[member] = supported;

        return supported;
    }

    /// <summary>Pooled gravgen lift over the set covers its pooled mass, or a member is on ground.</summary>
    private bool SetHasSupport(HashSet<EntityUid> set)
    {
        if (HasPooledGravgenSupport(set))
            return true;

        foreach (var member in set)
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
    /// support decision in <see cref="RigidSetHasSupport"/> — so the readout reflects
    /// whether the rigid stack actually stays aloft, not just this one grid. Unlike the
    /// gravity sweep this recomputes instead of reading the throttled cache, so it's never stale.
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
