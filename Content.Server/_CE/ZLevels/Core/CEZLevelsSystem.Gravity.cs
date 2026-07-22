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
    /// pzn: pooled MaxHandledMass of active gravgens per grid.
    /// </summary>
    private readonly Dictionary<EntityUid, float> _gravgenCapacity = new();
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

                if (_physQuery.TryComp(uid, out var body) && body.BodyType == BodyType.Static)
                    continue;

                // "Why not use IsWeightless" Doesn't work on grids. I tried.
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

            // Slow down when approaching a ground layer so people under you got some time to move.
            if (faller.Velocity > 0f && HasComp<CEZGroundLayerComponent>(lowerMap))
            {
                var cap = MathF.Max(TouchdownSpeed, progress * ApproachGain);
                if (faller.Velocity > cap)
                    faller.Velocity = MoveTowards(faller.Velocity, cap, damp * frameTime);
            }
        }

        foreach (var member in CollectGridSet(grid))
            SetZVelocity(member, -faller.Velocity);

        var altitude = lowerZ.Depth + progress - faller.Velocity * frameTime;
        if (!SetTransitAltitude(grid, altitude))
            return;

        // Still airborne?
        if (HasComp<CEZTransitMapComponent>(Transform(grid).MapUid))
            return;

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

    private bool GridHasActiveGravgen(EntityUid grid)
    {
        if (!_gravgenCapacity.TryGetValue(grid, out var capacity))
            return false;

        if (float.IsPositiveInfinity(capacity))
            return true;

        var mass = _physQuery.TryComp(grid, out var body) ? body.FixturesMass : 0f;
        return mass <= capacity;
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

        var query = EntityQueryEnumerator<GravityGeneratorComponent, TransformComponent>();
        while (query.MoveNext(out _, out var gravgen, out var xform))
        {
            if (!gravgen.GravityActive || xform.ParentUid != gridUid)
                continue;

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
