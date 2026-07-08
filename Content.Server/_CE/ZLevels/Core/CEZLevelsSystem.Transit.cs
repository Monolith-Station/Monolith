/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
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

    // TODO Mono: swap for the CEPvsOverrideComponent port when the PVS/viewer
    // task lands; the engine global override does the same job for grids.
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;

    private void InitializeTransit()
    {
        SubscribeLocalEvent<GridAddEvent>(OnGridAdd);
        SubscribeLocalEvent<MapGridComponent, EntParentChangedMessage>(OnGridParentChanged);
    }

    private void OnGridAdd(GridAddEvent ev)
    {
        RefreshGridZPhysics(ev.EntityUid);
    }

    private void OnGridParentChanged(Entity<MapGridComponent> ent, ref EntParentChangedMessage args)
    {
        RefreshGridZPhysics(ent);
    }

    /// <summary>
    /// Maps can join a z-network after their grids already loaded; those grids never saw a
    /// parent change, so sweep them for z-physics/faller state when the map joins.
    /// </summary>
    private void SweepMapGridsForZPhysics(EntityUid mapUid)
    {
        var children = Transform(mapUid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (HasComp<MapGridComponent>(child))
                RefreshGridZPhysics(child);
        }
    }

    /// <summary>
    /// Grids arriving on a z-network get z-physics and a gravity grace period; grids
    /// leaving it drop the gravity state so a return starts a fresh grace.
    /// </summary>
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
            if (ZPhysicsQuery.HasComp(child))
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
    /// changes; crossing a plane hops gaps (see <see cref="SetTransitAltitude"/>).
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

        if (!HasComp<CEZMapComponent>(currentMap))
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
            lowerMap = below.Owner;
            upperMap = currentMap;
            offset = -1;
            depth = below.Comp.Depth;
            defaultProgress = 1f;
        }
        else if (hasAbove)
        {
            lowerMap = currentMap;
            upperMap = above.Owner;
            offset = 1;
            depth = above.Comp.Depth;
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
            // TODO Mono: CEPvsOverrideComponent once the PVS port lands (task #3).
            _pvsOverride.AddGlobalOverride(gridUid);

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
            !TryComp<CEZMapComponent>(anchor, out var anchorZ))
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

            transitMapUid = HopTransitGap(grid, transitMapUid, upper, newUpper.Owner, 1, newUpper.Comp.Depth);
            transit = Comp<CEZTransitMapComponent>(transitMapUid);
            progress -= 1f;
        }

        while (progress < 0f)
        {
            // Ground layers stop descents dead — you land ON them, never hop past them.
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

            transitMapUid = HopTransitGap(grid, transitMapUid, newLower.Owner, lower, -1, newLower.Comp.Depth);
            transit = Comp<CEZTransitMapComponent>(transitMapUid);
            progress += 1f;
        }

        foreach (var gridUid in CollectGridSet(grid))
        {
            if (ZPhysicsQuery.TryComp(gridUid, out var zPhys))
                SetZPosition((gridUid, zPhys), progress);
        }

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

    private bool LandTransitSet(Entity<MapGridComponent> grid)
    {
        foreach (var gridUid in CollectGridSet(grid))
        {
            if (ZPhysicsQuery.TryComp(gridUid, out var zPhys))
                SetZPosition((gridUid, zPhys), 0f);
        }

        return TryExitTransit(grid);
    }

    private void RaiseExpandEvent(EntityUid edgeMap, bool up)
    {
        if (!TryGetMapNetwork(edgeMap, out var network))
            return;

        var ev = new CEZNetworkExpandRequestEvent(network, edgeMap, up);
        RaiseLocalEvent(network.Owner, ref ev);
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
        var up = ZPhysicsQuery.TryComp(grid, out var gridZPhys) && gridZPhys.LocalPosition >= 0.5f;

        var arrivalMap = up ? transit.UpperMap : transit.LowerMap;
        if (arrivalMap is not { } destination ||
            !TryComp<CEZMapComponent>(destination, out var destZMap))
        {
            return false;
        }

        var movedGrids = CollectGridSet(grid);
        MoveGridSetToMap(movedGrids, destination, up ? 1 : -1, destZMap.Depth);

        var parked = HasComp<CEZGroundLayerComponent>(destination);
        foreach (var gridUid in movedGrids)
        {
            if (ZPhysicsQuery.TryComp(gridUid, out var zPhys))
            {
                SetZPosition((gridUid, zPhys), 0f);
                SetZVelocity((gridUid, zPhys), 0f);
            }

            // TODO Mono: CEPvsOverrideComponent once the PVS port lands (task #3).
            _pvsOverride.RemoveGlobalOverride(gridUid);

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
        if (TryGetMapNetwork(lowerMap, out var network) && network.Comp.Components.Count > 0)
            EntityManager.AddComponents(mapUid, network.Comp.Components, removeExisting: false);

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
/// generation can extend the network during this event and the grid will continue
/// into the new level seamlessly instead of stopping.
/// </summary>
[ByRefEvent]
public record struct CEZNetworkExpandRequestEvent(
    Entity<CEZMapNetworkComponent> Network,
    EntityUid EdgeMap,
    bool Up);
