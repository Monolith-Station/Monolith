/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Server.Shuttles.Systems;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly DockingSystem _dockSystem = default!;
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    /// <inheritdoc/>
    protected override bool TryMoveGrid(Entity<MapGridComponent> grid,
        Entity<CEZLevelMapComponent, MapComponent> targetMap,
        int offset)
    {
        var gridXform = Transform(grid);
        var sourceMap = gridXform.MapUid;

        // Docked grids come along, keeping their relative position: all z-level maps share
        // one world coordinate space, so keeping world transforms preserves the formation.
        var movedGrids = new HashSet<EntityUid>();
        _shuttle.GetAllDockedShuttles(grid, movedGrids);
        movedGrids.Add(grid);

        // Docked chains can span maps in degenerate cases; only take grids from our own level.
        movedGrids.RemoveWhere(uid => Transform(uid).MapUid != sourceMap);

        foreach (var gridUid in movedGrids)
        {
            var beforeEv = new CEZLevelBeforeMapMoveEvent(offset, targetMap.Comp1.Depth);
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

            _transform.SetCoordinates(gridUid, xform, new EntityCoordinates(targetMap.Owner, worldPos), rotation: worldRot);

            if (body != null)
            {
                _physics.SetLinearVelocity(gridUid, linVel, body: body);
                _physics.SetAngularVelocity(gridUid, angVel, body: body);
            }
        }

        var ev = new CEZLevelMapMoveEvent(offset, targetMap.Comp1.Depth);
        foreach (var gridUid in movedGrids)
        {
            // Re-establish docking joints that the engine cleared during the map change.
            _dockSystem.RedockDocks(gridUid);
            _console.RefreshShuttleConsoles(gridUid);

            RaiseLocalEvent(gridUid, ref ev);
            RaiseZMoveEventOnPassengers(gridUid, ref ev);

            // The caller normalizes LocalPosition of the grid that initiated the move,
            // but docked companions have to be kept in sync here.
            if (gridUid != grid.Owner && ZPhyzQuery.TryComp(gridUid, out var zPhys))
                SetZPosition((gridUid, zPhys), zPhys.LocalPosition - offset);
        }

        return true;
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
}
