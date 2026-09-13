/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._CE.ZLevels.Core.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Gravity;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private GravitySystem _grav = default!;

    [Dependency] private EntityQuery<CEZMapComponent> _zMapQuery = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physQuery = default!;

    private readonly List<Entity<MapGridComponent>> _gravityQueue = new();

    /// <summary>
    /// pzn: pooled MaxHandledMass of active gravgens per grid.
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
    /// Drop grid if no gravgen.
    /// </summary>
    private void UpdateGridGravity(float frameTime)
    {
        CollectPilotVerticalInputs();
        UpdateTakeoffSpool();

        // Throttle checking for grid gravity so the server doesn't set itself on fire.
        if (_timing.CurTime >= _nextGravityCheckTime)
        {
            _nextGravityCheckTime = _timing.CurTime + _gravityCheckTimer;

            _gravityQueue.Clear();

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

                if (_physQuery.TryComp(uid, out var body) && body.BodyType == BodyType.Static)
                    continue;

                // "Why not use IsWeightless on each grid-" Doesn't work on grids. I tried.
                // This also covers "you can't fall out of the ground floor": a grid sat on the
                // bottom level's terrain has ground under its footprint like any other.
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

    private readonly List<(EntityUid Map, CEZTransitMapComponent Transit, EntityUid Primary, float Progress)> _transitCollisionScan = new();
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

                // If the maps didn't swap positions, they couldn't have collided (therefore, it is Not Our Problem)
                if (!_transitLastProgress.TryGetValue(a.Map, out var prevA) ||
                    !_transitLastProgress.TryGetValue(b.Map, out var prevB))
                {
                    continue; // first tick tracked for this pair
                }

                var prevDelta = prevA - prevB;
                var curDelta = a.Progress - b.Progress;

                if (prevDelta * curDelta > 0f)
                    continue;

                if (!TryGetGridSetAabb(a.Primary, out var setA, out var aabbA) ||
                    !TryGetGridSetAabb(b.Primary, out var setB, out var aabbB) ||
                    !aabbA.Intersects(aabbB))
                {
                    continue;
                }

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
    /// Someone forgot their gravgen.
    /// </summary>
    private static float ApproachTerminal(float velocity, float signedAccel, float terminalSpeed, float frameTime)
    {
        if (terminalSpeed <= 0f || signedAccel == 0f)
            return velocity;

        var speedInDir = signedAccel > 0f ? MathF.Max(0f, velocity) : MathF.Max(0f, -velocity);
        var taper = Math.Clamp(1f - speedInDir / terminalSpeed, 0f, 1f);
        return velocity + signedAccel * taper * frameTime;
    }

    /// <summary>
    /// Moves a value toward a target by at most <paramref name="maxDelta"/>.
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
                // Only settle up onto the level above if the convoy can actually get through it.
                // Pinned under solid terrain the ship just hovers against the underside; without
                // this it would drift into the touchdown band and pop out on top of the ceiling
                // that was blocking it.
                else if (progress >= 1f - SettleZone
                         && topUpperMap is { } upper
                         && !ConvoyBlockedByPlane(convoy[^1], upper))
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

            // Slow down when approaching terrain so people under you got some time to move.
            if (faller.Velocity > 0f && ConvoyBlockedByPlane(convoy[0], groundMapBelow))
            {
                var cap = MathF.Max(TouchdownSpeed, progress * ApproachGain);
                if (faller.Velocity > cap)
                    faller.Velocity = MoveTowards(faller.Velocity, cap, damp * frameTime);
            }
        }

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

        var impact = faller.Velocity;
        faller.Velocity = 0f;

        // Only a landing that ended ON terrain is a crash; setting down over open sky isn't.
        if (impact < faller.GridCrashVelocity
            || Transform(grid).MapUid is not { } landedMap
            || !HasGroundUnderFootprint(grid, landedMap))
        {
            return;
        }

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
    /// kaboom?
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
    /// Will It Lift?
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
    /// pzn: Get the current load of the grid for gravgen examine.
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

    /// <summary>
    /// Whether a z-level has solid terrain under (or over) a grid's footprint. A z-level map
    /// entity carries its own MapGrid and its tiles are the layer's terrain, so this is what
    /// makes a level something a ship can rest on or be stopped by.
    ///
    /// Solid is a non-empty tile — the same rule the entity fall path
    /// (<c>ComputeGroundHeightInternal</c>) uses, so a gap punched in a platform drops a ship
    /// through exactly like it drops a person instead of the two disagreeing about the same tile.
    ///
    /// Emptiness rather than <see cref="ContentTileDefinition.Transparent"/>, deliberately:
    /// transparency is a SIGHT property, driving roof occlusion and the look-up/look-down view, and
    /// a floor you can see through is still a floor. Lattice, glass floor and damaged plating are
    /// all transparent and all hold weight, so keying support off it dropped ships through tiles
    /// people were standing on. Space is tile id 0, so it is empty here anyway.
    /// </summary>
    private bool HasGroundUnderFootprint(Entity<MapGridComponent> grid, EntityUid mapUid)
    {
        if (!TryComp<MapGridComponent>(mapUid, out var mapGrid))
            return false;

        var worldAabb = _transform.GetWorldMatrix(grid).TransformBox(grid.Comp.LocalAABB);
        var tiles = _map.GetTilesEnumerator(mapUid, mapGrid, worldAabb);
        while (tiles.MoveNext(out var tileRef))
        {
            if (!tileRef.Tile.IsEmpty)
                return true;
        }

        return false;
    }
}
