/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Server._CE.PVS;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Shuttles.Systems;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Gravity;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly DockingSystem _dockSystem = default!;
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;

    /// <summary>
    /// Seconds after arriving on a z-network before gravity acts on a grid.
    /// </summary>
    private const float GridGravityGraceSeconds = 3f;

    /// <summary>
    /// Downward acceleration in levels per second squared. Falls start slow and build.
    /// </summary>
    private const float GridGravity = 0.15f;

    /// <summary>
    /// Maximum fall speed in levels per second.
    /// </summary>
    private const float GridTerminalVelocity = 1.2f;

    /// <summary>
    /// Touchdown speed at or above which a ground-layer landing is a crash.
    /// A full one-gap free fall arrives at ~0.74.
    /// </summary>
    private const float GridCrashVelocity = 0.35f;

    /// <summary>
    /// Roughly a 3x3 crater per hull tile on crash.
    /// </summary>
    private const float CrashTileIntensity = 12f;
    private const float CrashTileSlope = 2f;
    private const float CrashTileMaxIntensity = 4f;

    /// <summary>
    /// The central crash blast scales with hull size.
    /// </summary>
    private const float CrashIntensityPerTile = 5f;
    private const float CrashCenterSlope = 5f;
    private const float CrashCenterMaxIntensity = 100f;

    private readonly List<Entity<MapGridComponent>> _gravityQueue = new();

    private void InitializeGridMovement()
    {
        SubscribeLocalEvent<GridAddEvent>(OnGridAdd);
        SubscribeLocalEvent<MapGridComponent, EntParentChangedMessage>(OnGridParentChanged);
    }

    private void OnGridAdd(GridAddEvent ev)
    {
        RefreshGridFaller(ev.EntityUid);
    }

    private void OnGridParentChanged(Entity<MapGridComponent> ent, ref EntParentChangedMessage args)
    {
        RefreshGridFaller(ent);
    }

    /// <summary>
    /// Grids arriving on a z-network get z-physics and a gravity grace period; grids
    /// leaving it drop the gravity state so a return starts a fresh grace.
    /// </summary>
    private void RefreshGridFaller(EntityUid grid)
    {
        if (HasComp<MapComponent>(grid) || TerminatingOrDeleted(grid))
            return;

        var mapUid = Transform(grid).MapUid;
        var onZLevels = HasComp<CEZLevelMapComponent>(mapUid) || HasComp<CEZTransitMapComponent>(mapUid);

        if (!onZLevels)
        {
            RemComp<CEZGridFallerComponent>(grid);
            return;
        }

        EnsureComp<CEZPhysicsComponent>(grid);

        if (!HasComp<CEZGridFallerComponent>(grid))
        {
            var faller = AddComp<CEZGridFallerComponent>(grid);
            faller.GravityTime = _timing.CurTime + TimeSpan.FromSeconds(GridGravityGraceSeconds);
        }
    }

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

        // Collect first: entering/hopping transit adds components mid-query otherwise.
        _gravityQueue.Clear();

        var levelQuery = EntityQueryEnumerator<CEZGridFallerComponent, MapGridComponent, TransformComponent>();
        while (levelQuery.MoveNext(out var uid, out var faller, out var grid, out var xform))
        {
            if (_timing.CurTime < faller.GravityTime)
                continue;

            if (xform.MapUid is not { } mapUid || !HasComp<CEZLevelMapComponent>(mapUid))
                continue;

            // You can't fall out of the ground floor.
            if (HasComp<CEZGroundLayerComponent>(mapUid))
                continue;

            // Parked/anchored ships hold position.
            if (TryComp<PhysicsComponent>(uid, out var body) && body.BodyType == BodyType.Static)
                continue;

            if (TryComp<GravityComponent>(uid, out var gravity) && gravity.Enabled)
                continue;

            if (HasGroundUnderFootprint((uid, grid), mapUid))
                continue;

            _gravityQueue.Add((uid, grid));
        }

        foreach (var grid in _gravityQueue)
        {
            if (TryComp<CEZGridFallerComponent>(grid, out var faller))
                faller.Velocity = 0f;

            TryEnterTransit(grid);
        }

        // Falling grids in transit: one integrator per transit map, driving the whole
        // docked set through SetTransitAltitude (hops, crushes and landings included).
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

            // The debug wave rig owns this ship's altitude.
            if (HasComp<CEZTransitWaveComponent>(primary))
                continue;

            _gravityQueue.Add((primary, primaryGrid));
        }

        foreach (var grid in _gravityQueue)
        {
            IntegrateFallingGrid(grid, frameTime);
        }
    }

    private void IntegrateFallingGrid(Entity<MapGridComponent> grid, float frameTime)
    {
        if (!TryComp<CEZGridFallerComponent>(grid, out var faller) ||
            !ZPhyzQuery.TryComp(grid, out var zPhys))
        {
            return;
        }

        var xform = Transform(grid);
        if (!TryComp<CEZTransitMapComponent>(xform.MapUid, out var transit) ||
            transit.LowerMap is not { } lowerMap ||
            !TryComp<CEZLevelMapComponent>(lowerMap, out var lowerZ))
        {
            return;
        }

        var progress = zPhys.LocalPosition;
        var hasGravgen = TryComp<GravityComponent>(grid, out var gravity) && gravity.Enabled;

        if (!hasGravgen)
        {
            // Plummeting. The consoles can beep all they want.
            // (The grace period only delays gravity, never pilot control.)
            if (_timing.CurTime < faller.GravityTime)
                return;

            faller.Velocity = Math.Min(faller.Velocity + GridGravity * frameTime, GridTerminalVelocity);
        }
        else
        {
            var input = GetTransitVerticalInput(xform.MapUid!.Value);
            var accel = GetVerticalThrustAccel(grid);

            if (input > 0f && accel > 0f)
            {
                faller.Velocity = Math.Max(faller.Velocity - input * accel * frameTime, -MaxPilotVerticalSpeed);
            }
            else if (input < 0f && accel > 0f)
            {
                faller.Velocity = Math.Min(faller.Velocity - input * accel * frameTime, MaxPilotVerticalSpeed);
            }
            else
            {
                // The gravgen has the stick. Idle near a plane means "park on it";
                // idle mid-gap means hover.
                var damp = Math.Max(accel, HoverDampAccel);

                if (progress <= SettleZone)
                {
                    faller.Velocity = Math.Min(faller.Velocity + damp * frameTime, MaxPilotVerticalSpeed);

                    if (progress <= TouchdownProgress)
                    {
                        faller.Velocity = 0f;
                        TryExitTransit(grid);
                        return;
                    }
                }
                else if (progress >= 1f - SettleZone && transit.UpperMap != null)
                {
                    faller.Velocity = Math.Max(faller.Velocity - damp * frameTime, -MaxPilotVerticalSpeed);

                    if (progress >= 1f - TouchdownProgress)
                    {
                        faller.Velocity = 0f;
                        TryExitTransit(grid);
                        return;
                    }
                }
                else if (faller.Velocity > 0f)
                {
                    faller.Velocity = Math.Max(faller.Velocity - damp * frameTime, 0f);
                }
                else
                {
                    faller.Velocity = Math.Min(faller.Velocity + damp * frameTime, 0f);
                }
            }

            // Approach governor: any descent that ends ON the plane below — ground
            // layers always, settles too — slows the fuck down before touching it.
            // Punching through a non-ground plane with the key held stays fast.
            var landingBelow = HasComp<CEZGroundLayerComponent>(lowerMap) ||
                               (input == 0f && progress <= SettleZone);
            if (faller.Velocity > 0f && landingBelow)
                faller.Velocity = Math.Min(faller.Velocity, Math.Max(TouchdownSpeed, progress * ApproachGain));

            // Same gentle touch when settling up onto the plane above.
            if (faller.Velocity < 0f && input == 0f && progress >= 1f - SettleZone)
                faller.Velocity = Math.Max(faller.Velocity, -Math.Max(TouchdownSpeed, (1f - progress) * ApproachGain));
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

        if (impact < GridCrashVelocity || !HasComp<CEZGroundLayerComponent>(Transform(grid).MapUid))
            return;

        foreach (var landedUid in CollectGridSet(grid))
        {
            if (TryComp<MapGridComponent>(landedUid, out var landedGrid))
                CrashGrid((landedUid, landedGrid));
        }
    }

    /// <summary>
    /// A hard ground-layer touchdown: a small explosion on every hull tile plus one
    /// central blast scaled by hull size.
    /// </summary>
    private void CrashGrid(Entity<MapGridComponent> ent)
    {
        var tileCount = 0;
        var tiles = _map.GetAllTilesEnumerator(ent, ent.Comp);
        while (tiles.MoveNext(out var tileRef))
        {
            tileCount++;
            var coords = _map.GridTileToLocal(ent, ent.Comp, tileRef.Value.GridIndices);
            _explosion.QueueExplosion(coords,
                ExplosionSystem.DefaultExplosionPrototypeId,
                CrashTileIntensity,
                CrashTileSlope,
                CrashTileMaxIntensity,
                cause: ent,
                addLog: false);
        }

        if (tileCount == 0)
            return;

        _explosion.QueueExplosion(ent.Owner,
            ExplosionSystem.DefaultExplosionPrototypeId,
            CrashIntensityPerTile * tileCount,
            CrashCenterSlope,
            CrashCenterMaxIntensity);
    }

    /// <summary>
    /// Whether any solid tile of the map lies under the grid's footprint (the grid is
    /// resting on something rather than hanging over a hole or open sky).
    /// </summary>
    private bool HasGroundUnderFootprint(Entity<MapGridComponent> grid, EntityUid mapUid)
    {
        if (!TryComp<MapGridComponent>(mapUid, out var mapGrid))
            return false;

        var worldAabb = _transform.GetWorldMatrix(grid).TransformBox(grid.Comp.LocalAABB);
        var tiles = _map.GetTilesEnumerator(mapUid, mapGrid, worldAabb);
        return tiles.MoveNext(out _);
    }

    /// <inheritdoc/>
    protected override bool TryMoveGrid(Entity<MapGridComponent> grid,
        Entity<CEZLevelMapComponent, MapComponent> targetMap,
        int offset)
    {
        var movedGrids = CollectGridSet(grid);
        MoveGridSetToMap(movedGrids, targetMap.Owner, offset, targetMap.Comp1.Depth);

        foreach (var gridUid in movedGrids)
        {
            // Ships parked on the ground get their engines back once they're off the
            // ground layer (see CEZGroundLayerComponent).
            if (!HasComp<CEZGroundLayerComponent>(targetMap.Owner))
                _shuttle.Enable(gridUid);
        }

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
        movedGrids.RemoveWhere(uid => Transform(uid).MapUid != sourceMap);

        return movedGrids;
    }

    /// <summary>
    /// Moves a set of grids to another map, keeping world transforms (all z-level maps
    /// share one world coordinate space) and momentum, re-establishing the docking
    /// joints the engine clears on map changes, and notifying every passenger.
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
            if (ZPhyzQuery.HasComp(child))
                RaiseLocalEvent(child, ref ev);

            RaiseZMoveEventOnPassengers(child, ref ev);
        }
    }

    // ===== Transit maps: grids vertically between two z-levels =====

    /// <summary>
    /// Moves a grid (and its docked set) into a fresh transit map. Entry is
    /// direction-agnostic: a level's plane is both the top of the gap below and the
    /// bottom of the gap above, so the grid simply becomes airborne at its own plane
    /// (gap below at progress 1 when one exists, otherwise gap above at progress 0 —
    /// the same physical place). Which way it goes afterwards is just how its progress
    /// changes; crossing a plane hops gaps (see <see cref="SetTransitProgress"/>).
    /// Pass <paramref name="preferUpperGap"/> to become airborne in the gap above
    /// instead (a liftoff shouldn't sweep the pad it's leaving when it climbs
    /// through its own plane).
    /// </summary>
    public bool TryEnterTransit(Entity<MapGridComponent> grid, float? startProgress = null, bool preferUpperGap = false)
    {
        var xform = Transform(grid);

        if (xform.MapUid is not { } currentMap)
            return false;

        if (HasComp<CEZTransitMapComponent>(currentMap))
            return false; // Already in transit.

        if (!HasComp<CEZLevelMapComponent>(currentMap))
            return false;

        EntityUid lowerMap;
        EntityUid upperMap;
        int offset;
        int depth;
        float defaultProgress;

        var hasBelow = TryMapDown(currentMap, out var below);
        var hasAbove = TryMapUp(currentMap, out var above);

        if (hasBelow && !(preferUpperGap && hasAbove))
        {
            lowerMap = below!.Value.Owner;
            upperMap = currentMap;
            offset = -1;
            depth = below.Value.Comp.Depth;
            defaultProgress = 1f;
        }
        else if (hasAbove)
        {
            lowerMap = currentMap;
            upperMap = above!.Value.Owner;
            offset = 1;
            depth = above.Value.Comp.Depth;
            defaultProgress = 0f;
        }
        else
        {
            return false;
        }

        var transitMap = CreateTransitMap(lowerMap, upperMap, grid);

        var movedGrids = CollectGridSet(grid);
        MoveGridSetToMap(movedGrids, transitMap, offset, depth);

        var progress = Math.Clamp(startProgress ?? defaultProgress, 0f, 1f);
        foreach (var gridUid in movedGrids)
        {
            var zPhys = EnsureComp<CEZPhysicsComponent>(gridUid);
            SetZPosition((gridUid, zPhys), progress);

            // Everyone gets to watch, not just PVS neighbours.
            EnsureComp<CEPvsOverrideComponent>(gridUid);

            // In transit = airborne: engines are live regardless of direction
            // (parked ships are Static and need the re-enable to move at all).
            _shuttle.Enable(gridUid);
        }

        return true;
    }

    /// <summary>
    /// Sets a transiting grid set's altitude in the z-network's depth coordinates:
    /// the integer part is a level, the fraction is the position in the gap above it
    /// (1.1 = a tenth of a gap above level 1). Absolute and idempotent — repeating the
    /// same value keeps the ship where it is, however many gaps it crossed to get
    /// there. Values below the bottom of the network land the set on the bottom level.
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
            !TryComp<CEZLevelMapComponent>(anchor, out var anchorZ))
        {
            return false;
        }

        // Altitude is absolute; the current gap starts at its lower level's depth.
        var progress = altitude - anchorZ.Depth;

        while (progress > 1f)
        {
            if (transit.UpperMap is not { } upper)
            {
                progress = 1f;
                break;
            }

            if (!TryMapUp(upper, out var newUpper))
            {
                // Top of the network: give on-demand generation a chance to extend it
                // upward before clamping.
                RaiseExpandEvent(upper, up: true);

                if (!TryMapUp(upper, out newUpper))
                {
                    progress = 1f;
                    break;
                }
            }

            transitMapUid = HopTransitGap(grid, transitMapUid, upper, newUpper.Value.Owner, 1, newUpper.Value.Comp.Depth);
            transit = Comp<CEZTransitMapComponent>(transitMapUid);
            progress -= 1f;
        }

        while (progress < 0f)
        {
            // Ground layers stop falls dead — you land ON them, never hop past them.
            if (transit.LowerMap is not { } lower || HasComp<CEZGroundLayerComponent>(lower))
                return LandTransitSet(grid);

            if (!TryMapDown(lower, out var newLower))
            {
                // Bottom of the network without ground under it: give on-demand
                // generation (procgen cave layers) a chance to extend it downward
                // before concluding there's nothing there.
                RaiseExpandEvent(lower, up: false);

                if (!TryMapDown(lower, out newLower))
                    return LandTransitSet(grid);
            }

            transitMapUid = HopTransitGap(grid, transitMapUid, newLower.Value.Owner, lower, -1, newLower.Value.Comp.Depth);
            transit = Comp<CEZTransitMapComponent>(transitMapUid);
            progress += 1f;
        }

        foreach (var gridUid in CollectGridSet(grid))
        {
            if (ZPhyzQuery.TryComp(gridUid, out var zPhys))
                SetZPosition((gridUid, zPhys), progress);
        }

        return true;
    }

    /// <summary>
    /// Debug: toggles a sine-wave bob on a transiting grid around its current
    /// altitude. Enters transit first if the grid isn't already airborne.
    /// </summary>
    public bool ToggleTransitWave(Entity<MapGridComponent> grid, float amplitude, float period)
    {
        if (RemComp<CEZTransitWaveComponent>(grid))
            return true;

        var xform = Transform(grid);
        if (!TryComp<CEZTransitMapComponent>(xform.MapUid, out var transit))
        {
            if (!TryEnterTransit(grid))
                return false;

            xform = Transform(grid);
            transit = Comp<CEZTransitMapComponent>(xform.MapUid!.Value);
        }

        if (transit.LowerMap is not { } lower ||
            !TryComp<CEZLevelMapComponent>(lower, out var lowerZ) ||
            !ZPhyzQuery.TryComp(grid, out var zPhys))
        {
            return false;
        }

        var wave = AddComp<CEZTransitWaveComponent>(grid);
        wave.CenterAltitude = lowerZ.Depth + zPhys.LocalPosition;
        wave.Amplitude = amplitude;
        wave.Period = Math.Max(period, 1f);
        wave.StartTime = _timing.CurTime;

        return true;
    }

    /// <summary>
    /// Transit maps whose primary grid was deleted mid-gap (admin delete, crushed by
    /// another ship) have no exit path and would leak as phantom render passes.
    /// </summary>
    private void CleanupOrphanedTransitMaps()
    {
        var query = EntityQueryEnumerator<CEZTransitMapComponent>();
        while (query.MoveNext(out var uid, out var transit))
        {
            if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid))
                continue;

            if (transit.PrimaryGrid is { } primary &&
                !TerminatingOrDeleted(primary) &&
                Transform(primary).MapUid == uid)
            {
                continue;
            }

            QueueDel(uid);
            QueueAllViewerUpdates();
        }
    }

    private void UpdateTransitWaves()
    {
        var query = EntityQueryEnumerator<CEZTransitWaveComponent, MapGridComponent>();
        while (query.MoveNext(out var uid, out var wave, out var grid))
        {
            var t = (float) (_timing.CurTime - wave.StartTime).TotalSeconds;
            var altitude = wave.CenterAltitude + wave.Amplitude * MathF.Sin(MathF.Tau * t / wave.Period);

            // Left transit (landed, probably by dipping below the ground): wave over.
            if (!SetTransitAltitude((uid, grid), altitude))
                RemCompDeferred<CEZTransitWaveComponent>(uid);
        }
    }

    private bool LandTransitSet(Entity<MapGridComponent> grid)
    {
        foreach (var gridUid in CollectGridSet(grid))
        {
            if (ZPhyzQuery.TryComp(gridUid, out var zPhys))
                SetZPosition((gridUid, zPhys), 0f);
        }

        return TryExitTransit(grid);
    }

    private void RaiseExpandEvent(EntityUid edgeMap, bool up)
    {
        if (!TryZNetwork(edgeMap, out var network))
            return;

        var ev = new CEZNetworkExpandRequestEvent(network.Value, edgeMap, up);
        RaiseLocalEvent(network.Value.Owner, ref ev);
    }

    private EntityUid HopTransitGap(Entity<MapGridComponent> grid,
        EntityUid oldTransitMap,
        EntityUid lowerMap,
        EntityUid upperMap,
        int offset,
        int depth)
    {
        var newTransitMap = CreateTransitMap(lowerMap, upperMap, grid);

        var movedGrids = CollectGridSet(grid);
        MoveGridSetToMap(movedGrids, newTransitMap, offset, depth);

        // The hop sweeps the hull through a level's plane: going up that's the new
        // gap's floor, going down its ceiling. Whatever occupies the footprint there
        // gets flattened, FTL-style.
        var crossedPlane = offset > 0 ? lowerMap : upperMap;
        foreach (var gridUid in movedGrids)
        {
            _shuttle.Smimsh(gridUid, crushMap: crossedPlane, explodeGrids: true);
        }

        QueueDel(oldTransitMap);
        QueueAllViewerUpdates();

        return newTransitMap;
    }

    /// <summary>
    /// Lands a transiting grid set on whichever bordering z-level is nearest to its
    /// current progress and deletes the transit map. Ships arriving at a ground
    /// layer are parked.
    /// </summary>
    public bool TryExitTransit(Entity<MapGridComponent> grid)
    {
        var xform = Transform(grid);

        if (xform.MapUid is not { } transitMap)
            return false;

        if (!TryComp<CEZTransitMapComponent>(transitMap, out var transit))
            return false;

        // Nearest plane wins: exit is just "stop being airborne".
        var up = ZPhyzQuery.TryComp(grid, out var gridZPhys) && gridZPhys.LocalPosition >= 0.5f;

        var arrivalMap = up ? transit.UpperMap : transit.LowerMap;
        if (arrivalMap is not { } lowerMap ||
            !TryComp<CEZLevelMapComponent>(lowerMap, out var lowerZMap))
        {
            return false;
        }
        var movedGrids = CollectGridSet(grid);
        MoveGridSetToMap(movedGrids, lowerMap, up ? 1 : -1, lowerZMap.Depth);

        var parked = HasComp<CEZGroundLayerComponent>(lowerMap);
        foreach (var gridUid in movedGrids)
        {
            if (ZPhyzQuery.TryComp(gridUid, out var zPhys))
                SetZPosition((gridUid, zPhys), 0f);

            RemComp<CEPvsOverrideComponent>(gridUid);

            // Landing flattens whatever is under the hull, exactly like an FTL arrival —
            // except grids underneath blow apart in place instead of vanishing. The
            // rest of the landing set is exempt or docked ships would blast each other.
            _shuttle.Smimsh(gridUid, explodeGrids: true, ignoredGrids: movedGrids);

            if (parked)
            {
                _shuttle.Disable(gridUid);
                _console.RefreshShuttleConsoles(gridUid);
            }
        }

        // The set has left; a transit map only ever hosts one set.
        QueueDel(transitMap);
        QueueAllViewerUpdates();
        return true;
    }

    private EntityUid CreateTransitMap(EntityUid lowerMap, EntityUid upperMap, EntityUid primaryGrid)
    {
        var mapUid = _map.CreateMap(out _);

        var transit = AddComp<CEZTransitMapComponent>(mapUid);
        transit.LowerMap = lowerMap;
        transit.UpperMap = upperMap;
        transit.PrimaryGrid = primaryGrid;
        Dirty(mapUid, transit);

        // Same environment as the network's z-levels (atmosphere etc.), so crews don't
        // asphyxiate mid-descent.
        if (TryZNetwork(lowerMap, out var network) && network.Value.Comp.Components.Count > 0)
            EntityManager.AddComponents(mapUid, network.Value.Comp.Components, removeExisting: false);

        // Start lit like the upper level; the client lerps this toward the lower
        // level's ambient as the grid descends.
        var light = EnsureComp<MapLightComponent>(mapUid);
        if (TryComp<MapLightComponent>(upperMap, out var upperLight))
            light.AmbientLightColor = upperLight.AmbientLightColor;
        else if (TryComp<MapLightComponent>(lowerMap, out var lowerLight))
            light.AmbientLightColor = lowerLight.AmbientLightColor;
        Dirty(mapUid, light);

        _meta.SetEntityName(mapUid, $"Z-Transit above {MetaData(lowerMap).EntityName}");

        // Viewers near the gap need eyes on the new map (PVS + decal streaming).
        QueueAllViewerUpdates();

        return mapUid;
    }
}

/// <summary>
/// Raised on a z-network entity when a transiting grid reaches the network's edge with
/// nothing beyond it (and, going down, no ground layer to land on). On-demand layer
/// generation can extend the network during this event (e.g. via
/// <see cref="CEZLevelsSystem.TryAddMapBelowNetwork"/>) and the grid will continue
/// into the new level seamlessly instead of stopping.
/// </summary>
[ByRefEvent]
public record struct CEZNetworkExpandRequestEvent(
    Entity<CEZLevelsNetworkComponent> Network,
    EntityUid EdgeMap,
    bool Up);
