/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Physics;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Utility;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    [Dependency] private EntityQuery<FixturesComponent> _wallFixturesQuery = default!;
    public readonly record struct CEWallContact(Box2 ShipTile, Box2 WallTile);

    // The bounding box of the previous position of the grid.
    private Dictionary<EntityUid, (EntityUid Map, Box2Rotated Bounds)> _previousGridPosition = new();

    // Walls that may be colliding with a grid.
    private Dictionary<EntityUid, HashSet<EntityUid>> _wallsNearGrid = new();

    // In the event that grid processing ever becomes multithreaded; may God grant salvation to your soul.
    private List<Box2Rotated> _sweptArea = new();

    /// <summary>
    /// Every wall the hull is currently sitting on, on the z-level it occupies.
    /// </summary>
    [PublicAPI]
    public void GetWallContacts(EntityUid gridUid, List<CEWallContact> contacts)
    {
        if (!_gridQuery.TryComp(gridUid, out var gridComp))
            return;

        if (Transform(gridUid).MapUid is not { } map
            || map == gridUid
            || !_zMapQuery.HasComp(map)
            || !_gridQuery.TryComp(map, out var mapGrid))
        {
            return;
        }

        var (worldPos, worldRot) = _transform.GetWorldPositionRotation(gridUid);
        var current = new Box2Rotated(gridComp.LocalAABB.Translated(worldPos), worldRot, worldPos);

        var walls = _wallsNearGrid.GetOrNew(gridUid);

        if (_previousGridPosition.TryGetValue(gridUid, out var previous) && previous.Map == map)
        {
            if (!current.Equals(previous.Bounds)) // Don't rerun swept check on a non-moving grid.
            {
                _sweptArea.Clear();
                GetSweptArea(previous.Bounds, current, _sweptArea);

                foreach (var box in _sweptArea)
                    CacheWalls((map, mapGrid), box, walls);
            }
        }
        else
        {
            walls.Clear(); // Rechecking entire AABB; don't keep stale walls.
            CacheWalls((map, mapGrid), current, walls);
        }

        _previousGridPosition[gridUid] = (map, current);

        foreach (var wall in new List<EntityUid>(walls))
        {
            WallCollides((gridUid, gridComp), wall, walls, contacts);
        }
    }

    private void CacheWalls(Entity<MapGridComponent> map, Box2Rotated worldBounds, HashSet<EntityUid> walls)
    {
        foreach (var ent in _map.GetAnchoredEntities(map.Owner, map.Comp, worldBounds))
        {
            if (IsWall(ent))
                walls.Add(ent);
        }
    }

    private void WallCollides(Entity<MapGridComponent> grid, EntityUid wall, HashSet<EntityUid> walls, List<CEWallContact> contacts)
    {
        if (TerminatingOrDeleted(wall))
        {
            walls.Remove(wall); // Gone; not our problem
            return;
        }

        // Add an extra tile to the AABB so that we aren't constantly evicting tiles right at the edge of a grid.
        // Yes, I could just do 0.5; but 1 feels safer anyways.
        var bounds = _transform.GetWorldMatrix(grid.Owner).TransformBox(grid.Comp.LocalAABB).Enlarged(grid.Comp.TileSize);
        if (!bounds.Contains(_transform.GetWorldPosition(wall)))
        {
            walls.Remove(wall); // No longer within the grid bounding box; don't waste time on it anymore.
            return;
        }

        if (!_gridQuery.TryComp(Transform(wall).GridUid, out var wallGrid))
            return;

        var wallHalf = new Vector2(wallGrid.TileSize * 0.5f, wallGrid.TileSize * 0.5f);
        var wallCentre = _transform.GetWorldPosition(wall);
        var wallAabb = new Box2(wallCentre - wallHalf, wallCentre + wallHalf);

        var tileSize = grid.Comp.TileSize;
        var tileHalf = new Vector2(tileSize * 0.5f, tileSize * 0.5f);
        var gridMatrix = _transform.GetWorldMatrix(grid.Owner);

        foreach (var tile in _map.GetTilesIntersecting(grid.Owner, grid.Comp, wallAabb))
        {
            var localCentre = new Vector2(
                (tile.GridIndices.X + 0.5f) * tileSize,
                (tile.GridIndices.Y + 0.5f) * tileSize);

            var worldCentre = Vector2.Transform(localCentre, gridMatrix);
            var shipAabb = new Box2(worldCentre - tileHalf, worldCentre + tileHalf);

            if (shipAabb.Intersects(wallAabb))
                contacts.Add(new CEWallContact(shipAabb, wallAabb));
        }
    }

    /// <summary>Anything with a hard/impassable fixture is close enough to a wall, I guess.</summary>
    private bool IsWall(EntityUid uid)
    {
        if (!_wallFixturesQuery.TryComp(uid, out var fixtures))
            return false;

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (fixture.Hard && (fixture.CollisionLayer & (int)CollisionGroup.Impassable) != 0)
                return true;
        }

        return false;
    }
}
