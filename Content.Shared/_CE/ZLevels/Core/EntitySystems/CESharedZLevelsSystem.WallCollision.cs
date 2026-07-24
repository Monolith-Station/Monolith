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

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    [Dependency] private EntityQuery<FixturesComponent> _wallFixturesQuery = default!;

    /// <summary>
    /// One tile of a flying hull found sitting on a wall: the hull tile's world box and the terrain
    /// wall tile's world box it has driven into. The server resolves these into a push and rebound;
    /// the debug overlay draws them.
    /// </summary>
    public readonly record struct CEWallContact(Box2 ShipTile, Box2 WallTile);

    /// <summary>
    /// Every tile of a grid whose centre is over a wall on the z-level it occupies. Keyed on the
    /// hull's OWN tiles rather than its world AABB: a turned hull's bounding box juts out past the
    /// real tiles and would report walls the ship isn't actually touching, which is exactly the
    /// phantom blocking this replaced.
    ///
    /// Empty (and returns nothing) unless the grid is on a networked level that carries its own
    /// terrain grid — a transit map has no <see cref="CEZMapComponent"/>, so mid-flight grids fall
    /// through untouched.
    /// </summary>
    [PublicAPI]
    public void GetWallContacts(EntityUid gridUid, List<CEWallContact> contacts)
    {
        contacts.Clear();

        if (!_gridQuery.TryComp(gridUid, out var gridComp))
            return;

        var mapUid = Transform(gridUid).MapUid;
        if (mapUid is not { } map || !_zMapQuery.HasComp(map) || !_gridQuery.TryComp(map, out var mapGrid))
            return;

        // The terrain grid IS the map entity; it can't fly into its own walls.
        if (map == gridUid)
            return;

        var matrix = _transform.GetWorldMatrix(gridUid);
        var mapInvMatrix = _transform.GetInvWorldMatrix(map);

        var shipTileSize = gridComp.TileSize;
        var shipHalf = new Vector2(shipTileSize * 0.5f, shipTileSize * 0.5f);

        var mapTileSize = mapGrid.TileSize;
        var mapHalf = new Vector2(mapTileSize * 0.5f, mapTileSize * 0.5f);

        var tiles = _map.GetAllTilesEnumerator(gridUid, gridComp);
        while (tiles.MoveNext(out var shipTile))
        {
            var local = new Vector2(
                (shipTile.Value.GridIndices.X + 0.5f) * shipTileSize,
                (shipTile.Value.GridIndices.Y + 0.5f) * shipTileSize);
            var worldCentre = Vector2.Transform(local, matrix);
            var shipAabb = new Box2(worldCentre - shipHalf, worldCentre + shipHalf);

            const float edgeBias = 1e-4f;

            var localAabb = mapInvMatrix.TransformBox(shipAabb);
            var minX = (int)MathF.Floor(localAabb.Left / mapTileSize);
            var maxX = (int)MathF.Floor((localAabb.Right - edgeBias) / mapTileSize);
            var minY = (int)MathF.Floor(localAabb.Bottom / mapTileSize);
            var maxY = (int)MathF.Floor((localAabb.Top - edgeBias) / mapTileSize);

            for (var tx = minX; tx <= maxX; tx++)
            {
                for (var ty = minY; ty <= maxY; ty++)
                {
                    var wallTile = new Vector2i(tx, ty);
                    if (!TileHasWall((map, mapGrid), wallTile))
                        continue;

                    var wallCentre = _map.GridTileToWorldPos(map, mapGrid, wallTile);
                    var wallAabb = new Box2(wallCentre - mapHalf, wallCentre + mapHalf);

                    if (shipAabb.Intersects(wallAabb))
                        contacts.Add(new CEWallContact(shipAabb, wallAabb));
                }
            }
        }
    }

    private bool TileHasWall(Entity<MapGridComponent> map, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(map.Owner, map.Comp, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (IsWall(ent.Value))
                return true;
        }

        return false;
    }

    /// <summary>A hard fixture impassable at ground level counts as a wall — read off what mappers already place.</summary>
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
