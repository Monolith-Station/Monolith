/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Maps;
using Content.Shared.Physics;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Utility;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    [Dependency] private EntityQuery<FixturesComponent> _wallFixturesQuery = default!;
    private readonly Vector2[] _wallFootprint = new Vector2[PhysicsConstants.MaxPolygonVertices];
    private readonly Vector2[] _hullFootprint = new Vector2[PhysicsConstants.MaxPolygonVertices];
    private readonly Dictionary<EntityUid, (EntityUid Map, Box2Rotated Bounds)> _previousGridPosition = new();
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _wallsNearGrid = new();
    private readonly List<Box2Rotated> _sweptArea = new();
    private readonly List<EntityUid> _staleWalls = new();

    public readonly record struct CEWallContact(Vector2 Normal, float Depth);

    [PublicAPI]
    public void GetWallContacts(EntityUid gridUid, List<CEWallContact> contacts)
    {
        if (!_gridQuery.TryComp(gridUid, out var gridComp))
            return;

        if (Transform(gridUid).MapUid is not { } map
            || map == gridUid
            || !_zMapQuery.HasComp(map)
            || !_gridQuery.HasComp(map))
        {
            return;
        }

        var gridMatrix = _transform.GetWorldMatrix(gridUid);
        var toMap = gridMatrix * _transform.GetInvWorldMatrix(map);
        var origin = toMap.Translation;
        var rotation = _transform.GetWorldRotation(gridUid) - _transform.GetWorldRotation(map);
        var current = new Box2Rotated(gridComp.LocalAABB.Translated(origin), rotation, origin);
        var walls = _wallsNearGrid.GetOrNew(gridUid);
        if (!_previousGridPosition.TryGetValue(gridUid, out var previous) || previous.Map != map)
        {
            walls.Clear();
            CacheWalls(map, current, walls);
        }
        else if (!current.Equals(previous.Bounds))
        {
            _sweptArea.Clear();
            GetSweptArea(previous.Bounds, current, _sweptArea);
            foreach (var box in _sweptArea)
                CacheWalls(map, box, walls);
        }

        _previousGridPosition[gridUid] = (map, current);
        var bounds = gridMatrix.TransformBox(gridComp.LocalAABB);
        var cacheBounds = bounds.Enlarged(gridComp.TileSize);
        _staleWalls.Clear();
        foreach (var wall in walls)
        {
            if (TerminatingOrDeleted(wall) || Transform(wall) is not { Anchored: true } xform || xform.MapUid != map ||
                !AddWallContacts((gridUid, gridComp), wall, gridMatrix, bounds, cacheBounds, contacts))
            {
                _staleWalls.Add(wall);
            }
        }

        foreach (var wall in _staleWalls)
            walls.Remove(wall);
    }

    private void CacheWalls(EntityUid map, Box2Rotated bounds, HashSet<EntityUid> walls)
        => _lookup.GetLocalEntitiesIntersecting(map, bounds, walls, LookupFlags.Static | LookupFlags.Approximate);

    private bool AddWallContacts(Entity<MapGridComponent> grid, EntityUid wall, Matrix3x2 gridMatrix,
        Box2 bounds, Box2 cacheBounds, List<CEWallContact> contacts)
    {
        if (!_wallFixturesQuery.TryComp(wall, out var fixtures))
            return false;

        var wallMatrix = _transform.GetWorldMatrix(wall);
        var wallVertices = _wallFootprint.AsSpan();
        var nearby = false;
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!IsWallFixture(fixture))
                continue;

            var count = WriteFootprint(fixture.Shape, wallMatrix, wallVertices, out var radius);
            var footprint = wallVertices[..count];
            var wallBounds = GetFootprintBounds(footprint).Enlarged(radius);
            nearby |= cacheBounds.Intersects(wallBounds);
            if (!bounds.Intersects(wallBounds))
                continue;

            foreach (var tile in _map.GetTilesIntersecting(grid.Owner, grid.Comp, wallBounds))
                AddHullContacts(grid, tile, gridMatrix, footprint, radius, contacts);
        }

        return nearby;
    }

    public bool GridFootprintOverlapsEntity(Entity<MapGridComponent> grid, EntityUid target)
    {
        var gridMatrix = _transform.GetWorldMatrix(grid.Owner);
        var footprint = _wallFootprint.AsSpan();
        var hasHardFixture = false;
        if (_wallFixturesQuery.TryComp(target, out var fixtures))
        {
            var matrix = _transform.GetWorldMatrix(target);
            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard)
                    continue;

                hasHardFixture = true;
                var count = WriteFootprint(fixture.Shape, matrix, footprint, out var radius);
                if (GridFootprintOverlapsShape(grid, gridMatrix, footprint[..count], radius))
                    return true;
            }
        }

        if (hasHardFixture)
            return false;

        footprint[0] = _transform.GetWorldPosition(target);
        return GridFootprintOverlapsShape(grid, gridMatrix, footprint[..1], 0f);
    }

    private bool GridFootprintOverlapsShape(Entity<MapGridComponent> grid, Matrix3x2 matrix,
        ReadOnlySpan<Vector2> footprint, float radius)
    {
        var bounds = GetFootprintBounds(footprint).Enlarged(radius);
        foreach (var tile in _map.GetTilesIntersecting(grid.Owner, grid.Comp, bounds))
        {
            var count = WriteTileFootprint(tile, grid.Comp.TileSize, matrix, _hullFootprint);
            if (TryGetWallContact(_hullFootprint.AsSpan(0, count), 0f, footprint, radius, out _))
                return true;
        }

        return false;
    }

    protected static Box2 GetFootprintBounds(ReadOnlySpan<Vector2> footprint)
    {
        var min = footprint[0];
        var max = min;
        foreach (var vertex in footprint[1..])
        {
            min = Vector2.Min(min, vertex);
            max = Vector2.Max(max, vertex);
        }

        return new Box2(min, max);
    }

    private void AddHullContacts(Entity<MapGridComponent> grid, TileRef tile, Matrix3x2 gridMatrix,
        ReadOnlySpan<Vector2> wall, float wallRadius, List<CEWallContact> contacts)
    {
        var ship = _hullFootprint.AsSpan();
        var anchored = _map.GetAnchoredEntitiesEnumerator(grid.Owner, grid.Comp, tile.GridIndices);
        var hasFixtures = false;
        while (anchored.MoveNext(out var entity))
        {
            if (!_wallFixturesQuery.TryComp(entity, out var fixtures))
                continue;

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!IsWallFixture(fixture))
                    continue;

                hasFixtures = true;
                var count = WriteFootprint(fixture.Shape, _transform.GetWorldMatrix(entity.Value), ship, out var radius);
                if (TryGetWallContact(ship[..count], radius, wall, wallRadius, out var contact))
                    contacts.Add(contact);
            }
        }

        if (hasFixtures)
            return;

        var tileCount = WriteTileFootprint(tile, grid.Comp.TileSize, gridMatrix, ship);
        if (TryGetWallContact(ship[..tileCount], 0f, wall, wallRadius, out var floorContact))
            contacts.Add(floorContact);
    }

    protected int WriteTileFootprint(TileRef tile, float size, Matrix3x2 matrix, Span<Vector2> buffer)
    {
        var vertices = ((ContentTileDefinition) TilDefMan[tile.Tile.TypeId]).Vertices;
        var origin = new Vector2(tile.GridIndices.X, tile.GridIndices.Y);
        if (vertices.Count < 3 || vertices.Count > buffer.Length)
        {
            WriteTileBox(Box2.FromDimensions(origin * size, new Vector2(size)), buffer);
            for (var i = 0; i < 4; i++)
                buffer[i] = Vector2.Transform(buffer[i], matrix);
            return 4;
        }

        for (var i = 0; i < vertices.Count; i++)
            buffer[i] = Vector2.Transform((origin + vertices[i]) * size, matrix);
        return vertices.Count;
    }

    private static int WriteFootprint(IPhysShape shape, Matrix3x2 matrix, Span<Vector2> vertices, out float radius)
    {
        radius = 0f;
        int count;
        switch (shape)
        {
            case PolygonShape polygon:
                count = polygon.VertexCount;
                polygon.Vertices.AsSpan().CopyTo(vertices);
                break;
            case PhysShapeCircle circle:
                radius = circle.Radius;
                vertices[0] = Vector2.Transform(circle.Position, matrix);
                return 1;
            default:
                count = 4;
                var box = shape is PhysShapeAabb aabb
                    ? aabb.LocalBounds
                    : shape.ComputeAABB(new Robust.Shared.Physics.Transform(Vector2.Zero, 0f), 0);
                WriteTileBox(box, vertices);
                break;
        }

        for (var i = 0; i < count; i++)
            vertices[i] = Vector2.Transform(vertices[i], matrix);

        return count;
    }

    private static void WriteTileBox(Box2 box, Span<Vector2> vertices)
    {
        vertices[0] = box.BottomLeft;
        vertices[1] = box.TopLeft;
        vertices[2] = box.TopRight;
        vertices[3] = box.BottomRight;
    }

    protected static bool TryGetWallContact(ReadOnlySpan<Vector2> ship, float shipRadius,
        ReadOnlySpan<Vector2> wall, float wallRadius, out CEWallContact contact)
    {
        var normal = Vector2.Zero;
        var depth = float.PositiveInfinity;
        var hit = CheckAxes(ship, ship, shipRadius, wall, wallRadius, ref normal, ref depth) &&
                  CheckAxes(wall, ship, shipRadius, wall, wallRadius, ref normal, ref depth);
        if (hit && (ship.Length == 1 || wall.Length == 1))
        {
            var circle = ship.Length == 1 ? ship[0] : wall[0];
            var polygon = ship.Length == 1 ? wall : ship;
            var closest = polygon[0];
            foreach (var vertex in polygon[1..])
            {
                if (Vector2.DistanceSquared(vertex, circle) < Vector2.DistanceSquared(closest, circle))
                    closest = vertex;
            }

            hit = CheckAxis(circle == closest ? Vector2.UnitX : circle - closest,
                ship, shipRadius, wall, wallRadius, ref normal, ref depth);
        }

        contact = new CEWallContact(normal, depth);
        return hit;

        static bool CheckAxes(ReadOnlySpan<Vector2> edges, ReadOnlySpan<Vector2> ship, float shipRadius,
            ReadOnlySpan<Vector2> wall, float wallRadius, ref Vector2 normal, ref float depth)
        {
            if (edges.Length == 1)
                return true;

            for (var i = 0; i < edges.Length; i++)
            {
                var edge = edges[(i + 1) % edges.Length] - edges[i];
                if (!CheckAxis(new Vector2(-edge.Y, edge.X), ship, shipRadius, wall, wallRadius, ref normal, ref depth))
                    return false;
            }

            return true;
        }

        static bool CheckAxis(Vector2 axis, ReadOnlySpan<Vector2> ship, float shipRadius,
            ReadOnlySpan<Vector2> wall, float wallRadius, ref Vector2 normal, ref float depth)
        {
            axis = Vector2.Normalize(axis);
            var (shipMin, shipMax) = Project(ship, axis);
            var (wallMin, wallMax) = Project(wall, axis);
            var negative = shipMax - wallMin;
            var positive = wallMax - shipMin;
            var overlap = MathF.Min(negative, positive) + shipRadius + wallRadius;
            if (overlap <= 0f)
                return false;

            if (overlap < depth)
            {
                depth = overlap;
                normal = negative < positive ? -axis : axis;
            }

            return true;
        }

        static (float Min, float Max) Project(ReadOnlySpan<Vector2> polygon, Vector2 axis)
        {
            var min = Vector2.Dot(polygon[0], axis);
            var max = min;
            foreach (var vertex in polygon[1..])
            {
                var projected = Vector2.Dot(vertex, axis);
                min = MathF.Min(min, projected);
                max = MathF.Max(max, projected);
            }

            return (min, max);
        }
    }

    private static bool IsWallFixture(Fixture fixture)
    {
        const CollisionGroup fullHeight = CollisionGroup.LowImpassable | CollisionGroup.MidImpassable | CollisionGroup.HighImpassable;
        var layer = (CollisionGroup) fixture.CollisionLayer;
        return fixture.Hard && ((layer & CollisionGroup.Impassable) != 0 || (layer & fullHeight) == fullHeight);
    }
}
