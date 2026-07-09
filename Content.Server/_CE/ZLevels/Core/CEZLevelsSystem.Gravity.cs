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
        var hasGravgen = GridHasActiveGravgen(grid);

        if (!hasGravgen)
        {
            // AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
            if (_timing.CurTime < faller.GravityTime)
                return;

            faller.Velocity = ApproachTerminal(faller.Velocity, faller.GridGravity, faller.GridTerminalVelocity, frameTime);
        }
        else
        {
            var input = GetTransitVerticalInput(xform.MapUid!.Value);
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
                else if (progress >= 1f - SettleZone && transit.UpperMap != null)
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
            if (faller.Velocity > 0f && HasComp<CEZGroundLayerComponent>(lowerMap))
            {
                var cap = MathF.Max(TouchdownSpeed, progress * ApproachGain);
                if (faller.Velocity > cap)
                    faller.Velocity = MoveTowards(faller.Velocity, cap, damp * frameTime);
            }
        }

        // Mirror the fall speed onto the networked z-physics velocity (which uses the
        // opposite sign: positive = up) so consoles can read it. Whole set, since a
        // console may sit on a docked companion.
        foreach (var member in CollectGridSet(grid))
            SetZVelocity(member, -faller.Velocity);

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

        foreach (var landedUid in CollectGridSet(grid))
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
    /// pzn: on-demand load readout for examine/UI: the grid's physics mass plus the
    /// pooled rated capacity of every *active* gravgen on it, same rules as the
    /// gravity sweep (negative = unlimited, 0 = no lift hardware). Unlike <see cref="GridHasActiveGravgen"/>
    /// this recomputes instead of reading the throttled cache, so it's never stale.
    /// </summary>
    public bool TryGetGravgenLoad(EntityUid gridUid, out float gridMass, out float capacity)
    {
        gridMass = 0f;
        capacity = 0f;

        if (!HasComp<MapGridComponent>(gridUid))
            return false;

        var query = EntityQueryEnumerator<GravityGeneratorComponent, TransformComponent>();
        while (query.MoveNext(out _, out var gravgen, out var xform))
        {
            if (!gravgen.GravityActive || xform.ParentUid != gridUid)
                continue;

            // pzn: negative rating = infinite lift; 0 = pure gravity, no lift hardware at all.
            capacity += gravgen.MaxHandledMass < 0f ? float.PositiveInfinity : gravgen.MaxHandledMass;
        }

        if (_physQuery.TryComp(gridUid, out var body))
            gridMass = body.FixturesMass;

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
