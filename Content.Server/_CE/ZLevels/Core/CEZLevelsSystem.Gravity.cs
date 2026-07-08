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
    private readonly HashSet<EntityUid> _gravgenHeldGrids = new();
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
            // GravitySystem.RefreshGravity scans for. Precompute the set of grids with
            // an active generator so the gate below stays O(grids + gravgens).
            _gravgenHeldGrids.Clear();
            var gravgenQuery = EntityQueryEnumerator<GravityGeneratorComponent, TransformComponent>();
            while (gravgenQuery.MoveNext(out _, out var gravgen, out var gravgenXform))
            {
                if (gravgen.GravityActive && gravgenXform.ParentUid.IsValid())
                    _gravgenHeldGrids.Add(gravgenXform.ParentUid);
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
                if (_gravgenHeldGrids.Contains(uid))
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
                    if (progress <= TouchdownProgress)
                    {
                        faller.Velocity = 0f;
                        TryExitTransit(grid);
                        return;
                    }

                    target = MathF.Max(TouchdownSpeed, progress * ApproachGain);
                }
                else if (progress >= 1f - SettleZone && transit.UpperMap != null)
                {
                    if (progress >= 1f - TouchdownProgress)
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
    /// Same check GravitySystem effectively does when refreshing grid gravity:
    /// is there a gravgen parented to this grid that's actually producing gravity?
    /// </summary>
    private bool GridHasActiveGravgen(EntityUid grid)
    {
        var query = EntityQueryEnumerator<GravityGeneratorComponent, TransformComponent>();
        while (query.MoveNext(out _, out var gravgen, out var xform))
        {
            if (gravgen.GravityActive && xform.ParentUid == grid)
                return true;
        }

        return false;
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
