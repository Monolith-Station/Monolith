/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public sealed class CEZLevelOpeningCache(int chunkSize = CEZLevelOpeningCache.DefaultChunkSize)
{
    private const int DefaultChunkSize = 8;

    private readonly Dictionary<EntityUid, GridOpeningCache> _gridCaches = new();

    public int ChunkSize => chunkSize;

    public void Clear()
    {
        _gridCaches.Clear();
    }

    public void RemoveGrid(EntityUid grid)
    {
        _gridCaches.Remove(grid);
    }

    public void InvalidateTiles(Entity<MapGridComponent> grid, ReadOnlySpan<TileChangedEntry> changes)
    {
        if (!_gridCaches.TryGetValue(grid.Owner, out var cache))
            return;

        cache.LastTileModifiedTick = grid.Comp.LastTileModifiedTick;

        if (changes.Length == 0)
        {
            cache.Chunks.Clear();
            return;
        }

        for (var i = 0; i < changes.Length; i++)
        {
            var chunk = SharedMapSystem.GetChunkIndices(changes[i].GridIndices, chunkSize);
            cache.Chunks.Remove(chunk);
        }
    }

    public bool ChunkHasOpening(
        Entity<MapGridComponent> grid,
        Vector2i chunk,
        SharedMapSystem map,
        ITileDefinitionManager tile)
    {
        return GetChunkOpenings(grid, chunk, map, tile).HasOpening;
    }

    public bool HasOpeningInTileBounds(
        Entity<MapGridComponent> grid,
        Vector2i start,
        Vector2i end,
        SharedMapSystem map,
        ITileDefinitionManager tile)
    {
        var startX = Math.Min(start.X, end.X);
        var endX = Math.Max(start.X, end.X);
        var startY = Math.Min(start.Y, end.Y);
        var endY = Math.Max(start.Y, end.Y);

        var startChunk = SharedMapSystem.GetChunkIndices(new Vector2i(startX, startY), chunkSize);
        var endChunk = SharedMapSystem.GetChunkIndices(new Vector2i(endX, endY), chunkSize);

        for (var x = startChunk.X; x <= endChunk.X; x++)
        {
            for (var y = startChunk.Y; y <= endChunk.Y; y++)
            {
                if (ForEachOpeningTileInBounds(
                        grid,
                        new Vector2i(x, y),
                        startX,
                        endX,
                        startY,
                        endY,
                        map,
                        tile,
                        _ => true))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool TryFindOpeningBounds(
        MapId mapId,
        Box2 worldAabb,
        List<Box2>? openingBounds,
        out Box2 combinedOpeningBounds,
        int maxOpeningBounds,
        bool exactOpeningBounds,
        List<Entity<MapGridComponent>> gridScratch,
        IMapManager mapManager,
        SharedMapSystem map,
        SharedTransformSystem transform,
        ITileDefinitionManager tileDefinition)
    {
        combinedOpeningBounds = default;
        gridScratch.Clear();

        mapManager.FindGridsIntersecting(mapId, worldAabb, ref gridScratch, approx: true, includeMap: true);
        if (gridScratch.Count == 0)
            return false;

        var foundOpening = false;
        var combinedBounds = combinedOpeningBounds;
        var bottomLeft = new MapCoordinates(worldAabb.BottomLeft, mapId);
        var topRight = new MapCoordinates(worldAabb.TopRight, mapId);

        foreach (var grid in gridScratch)
        {
            GetTileSearchBounds(grid, bottomLeft, topRight, map, out var startX, out var endX, out var startY, out var endY);
            var gridWorldMatrix = transform.GetWorldMatrix(grid.Owner);

            var startChunk = SharedMapSystem.GetChunkIndices(new Vector2i(startX, startY), chunkSize);
            var endChunk = SharedMapSystem.GetChunkIndices(new Vector2i(endX, endY), chunkSize);

            for (var chunkX = startChunk.X; chunkX <= endChunk.X; chunkX++)
            {
                for (var chunkY = startChunk.Y; chunkY <= endChunk.Y; chunkY++)
                {
                    var chunk = new Vector2i(chunkX, chunkY);
                    if (!ChunkHasOpening(grid, chunk, map, tileDefinition))
                        continue;

                    if (openingBounds == null)
                        return true;

                    if (!exactOpeningBounds)
                    {
                        var chunkStart = chunk * chunkSize;
                        var chunkEnd = chunkStart + new Vector2i(chunkSize, chunkSize);
                        var localBounds = new Box2(chunkStart.X, chunkStart.Y, chunkEnd.X, chunkEnd.Y);
                        var worldBounds = gridWorldMatrix.TransformBox(localBounds);

                        AddOpeningBounds(openingBounds, worldBounds, ref combinedBounds, ref foundOpening);
                        if (openingBounds.Count >= maxOpeningBounds)
                        {
                            combinedOpeningBounds = combinedBounds;
                            return true;
                        }

                        continue;
                    }

                    var tileStart = chunk * chunkSize;
                    var tileEnd = tileStart + new Vector2i(chunkSize, chunkSize);
                    var tileStartX = Math.Max(startX, tileStart.X);
                    var tileEndX = Math.Min(endX, tileEnd.X - 1);
                    var tileStartY = Math.Max(startY, tileStart.Y);
                    var tileEndY = Math.Min(endY, tileEnd.Y - 1);

                    if (ForEachOpeningTileInBounds(
                            grid,
                            chunk,
                            tileStartX,
                            tileEndX,
                            tileStartY,
                            tileEndY,
                            map,
                            tileDefinition,
                            openingTile =>
                            {
                                var localTileBounds = new Box2(
                                    openingTile.X,
                                    openingTile.Y,
                                    openingTile.X + 1,
                                    openingTile.Y + 1);
                                var worldTileBounds = gridWorldMatrix.TransformBox(localTileBounds);
                                AddOpeningBounds(openingBounds, worldTileBounds, ref combinedBounds, ref foundOpening);

                                return openingBounds.Count >= maxOpeningBounds;
                            }))
                    {
                        combinedOpeningBounds = combinedBounds;
                        return true;
                    }
                }
            }
        }

        combinedOpeningBounds = combinedBounds;
        return foundOpening;
    }

    public void FindOpeningCentersNear(
        MapId mapId,
        Vector2 sourcePosition,
        float searchRadius,
        List<(Vector2 Center, float Distance)> openings,
        List<Entity<MapGridComponent>> gridScratch,
        IMapManager mapManager,
        SharedMapSystem map,
        SharedTransformSystem transform,
        ITileDefinitionManager tileDefinition,
        bool edgeOnly = true)
    {
        var searchBounds = Box2.CenteredAround(sourcePosition, new Vector2(searchRadius * 2f, searchRadius * 2f));
        gridScratch.Clear();
        mapManager.FindGridsIntersecting(mapId, searchBounds, ref gridScratch, approx: true, includeMap: true);

        if (gridScratch.Count == 0)
            return;

        var bottomLeft = new MapCoordinates(searchBounds.BottomLeft, mapId);
        var topRight = new MapCoordinates(searchBounds.TopRight, mapId);
        var searchRadiusSquared = searchRadius * searchRadius;

        foreach (var grid in gridScratch)
        {
            GetTileSearchBounds(grid, bottomLeft, topRight, map, out var startX, out var endX, out var startY, out var endY);

            var startChunk = SharedMapSystem.GetChunkIndices(new Vector2i(startX, startY), chunkSize);
            var endChunk = SharedMapSystem.GetChunkIndices(new Vector2i(endX, endY), chunkSize);
            var gridWorldMatrix = transform.GetWorldMatrix(grid.Owner);
            if (!Matrix3x2.Invert(gridWorldMatrix, out var gridInvWorldMatrix))
                continue;

            var localSourcePosition = Vector2.Transform(sourcePosition, gridInvWorldMatrix);
            var sourceInsideOpening = IsExistingOpeningTile(
                grid,
                new Vector2i((int) MathF.Floor(localSourcePosition.X), (int) MathF.Floor(localSourcePosition.Y)),
                map,
                tileDefinition);

            for (var chunkX = startChunk.X; chunkX <= endChunk.X; chunkX++)
            {
                for (var chunkY = startChunk.Y; chunkY <= endChunk.Y; chunkY++)
                {
                    var chunk = new Vector2i(chunkX, chunkY);
                    if (!ChunkHasOpening(grid, chunk, map, tileDefinition))
                        continue;

                    var chunkStart = chunk * chunkSize;
                    var chunkEnd = chunkStart + new Vector2i(chunkSize, chunkSize);
                    var tileStartX = Math.Max(startX, chunkStart.X);
                    var tileEndX = Math.Min(endX, chunkEnd.X - 1);
                    var tileStartY = Math.Max(startY, chunkStart.Y);
                    var tileEndY = Math.Min(endY, chunkEnd.Y - 1);

                    ForEachOpeningTileInBounds(
                        grid,
                        chunk,
                        tileStartX,
                        tileEndX,
                        tileStartY,
                        tileEndY,
                        map,
                        tileDefinition,
                        openingTile =>
                        {
                            if (edgeOnly &&
                                !IsOpeningEdgeTile(grid, openingTile, localSourcePosition, sourceInsideOpening, map, tileDefinition))
                            {
                                return false;
                            }

                            var center = Vector2.Transform(
                                new Vector2(openingTile.X + 0.5f, openingTile.Y + 0.5f),
                                gridWorldMatrix);
                            var distanceSquared = Vector2.DistanceSquared(sourcePosition, center);
                            if (distanceSquared > searchRadiusSquared)
                                return false;

                            openings.Add((center, MathF.Sqrt(distanceSquared)));
                            return false;
                        });
                }
            }
        }
    }

    public bool TryFindNearestOpeningCenterNear(
        MapId mapId,
        Vector2 sourcePosition,
        float searchRadius,
        out Vector2 openingCenter,
        List<Entity<MapGridComponent>> gridScratch,
        IMapManager mapManager,
        SharedMapSystem map,
        SharedTransformSystem transform,
        ITileDefinitionManager tileDefinition,
        bool edgeOnly = true)
    {
        openingCenter = default;

        var searchBounds = Box2.CenteredAround(sourcePosition, new Vector2(searchRadius * 2f, searchRadius * 2f));
        gridScratch.Clear();
        mapManager.FindGridsIntersecting(mapId, searchBounds, ref gridScratch, approx: true, includeMap: true);

        if (gridScratch.Count == 0)
            return false;

        var bottomLeft = new MapCoordinates(searchBounds.BottomLeft, mapId);
        var topRight = new MapCoordinates(searchBounds.TopRight, mapId);
        var searchRadiusSquared = searchRadius * searchRadius;
        var bestDistanceSquared = float.PositiveInfinity;
        var foundOpening = false;

        foreach (var grid in gridScratch)
        {
            GetTileSearchBounds(grid, bottomLeft, topRight, map, out var startX, out var endX, out var startY, out var endY);

            var startChunk = SharedMapSystem.GetChunkIndices(new Vector2i(startX, startY), chunkSize);
            var endChunk = SharedMapSystem.GetChunkIndices(new Vector2i(endX, endY), chunkSize);
            var gridWorldMatrix = transform.GetWorldMatrix(grid.Owner);
            if (!Matrix3x2.Invert(gridWorldMatrix, out var gridInvWorldMatrix))
                continue;

            var localSourcePosition = Vector2.Transform(sourcePosition, gridInvWorldMatrix);
            var sourceInsideOpening = IsExistingOpeningTile(
                grid,
                new Vector2i((int) MathF.Floor(localSourcePosition.X), (int) MathF.Floor(localSourcePosition.Y)),
                map,
                tileDefinition);

            for (var chunkX = startChunk.X; chunkX <= endChunk.X; chunkX++)
            {
                for (var chunkY = startChunk.Y; chunkY <= endChunk.Y; chunkY++)
                {
                    var chunk = new Vector2i(chunkX, chunkY);
                    var chunkStart = chunk * chunkSize;
                    var chunkEnd = chunkStart + new Vector2i(chunkSize, chunkSize);
                    var tileStartX = Math.Max(startX, chunkStart.X);
                    var tileEndX = Math.Min(endX, chunkEnd.X - 1);
                    var tileStartY = Math.Max(startY, chunkStart.Y);
                    var tileEndY = Math.Min(endY, chunkEnd.Y - 1);

                    TryFindNearestOpeningCenterInChunk(
                        grid,
                        chunk,
                        tileStartX,
                        tileEndX,
                        tileStartY,
                        tileEndY,
                        sourcePosition,
                        localSourcePosition,
                        sourceInsideOpening,
                        gridWorldMatrix,
                        searchRadiusSquared,
                        edgeOnly,
                        map,
                        tileDefinition,
                        ref foundOpening,
                        ref bestDistanceSquared,
                        ref openingCenter);
                }
            }
        }

        return foundOpening;
    }

    public static bool IsOpeningTile(
        Tile tile,
        ITileDefinitionManager tileDefinition)
    {
        if (tile.IsEmpty)
            return true;

        var tileDef = (ContentTileDefinition) tileDefinition[tile.TypeId];
        return tileDef.Transparent;
    }

    public static bool IsOpeningTile(
        Entity<MapGridComponent> grid,
        Vector2i tile,
        SharedMapSystem map,
        ITileDefinitionManager tileDefinition)
    {
        if (!map.TryGetTileRef(grid.Owner, grid.Comp, tile, out var tileRef))
            return true;

        return IsOpeningTile(tileRef.Tile, tileDefinition);
    }

    public static bool IsOpeningTile(
        EntityUid mapUid,
        MapGridComponent grid,
        Vector2 position,
        SharedMapSystem map,
        ITileDefinitionManager tileDefinition)
    {
        if (!map.TryGetTileRef(mapUid, grid, position, out var tileRef))
            return true;

        return IsOpeningTile(tileRef.Tile, tileDefinition);
    }

    public static bool IsExistingOpeningTile(
        Entity<MapGridComponent> grid,
        Vector2i tile,
        SharedMapSystem map,
        ITileDefinitionManager tileDefinition)
    {
        if (!map.TryGetTileRef(grid.Owner, grid.Comp, tile, out var tileRef))
            return false;

        return IsOpeningTile(tileRef.Tile, tileDefinition);
    }

    public static bool IsOpeningEdgeTile(
        Entity<MapGridComponent> grid,
        Vector2i tile,
        Vector2 localSourcePosition,
        bool sourceInsideOpening,
        SharedMapSystem map,
        ITileDefinitionManager tileDefinition)
    {
        if (sourceInsideOpening)
            return IsOpeningPerimeterTile(grid, tile, map, tileDefinition);

        var localCenter = new Vector2(tile.X + 0.5f, tile.Y + 0.5f);
        var directionToSource = localSourcePosition - localCenter;
        if (directionToSource.LengthSquared() < 0.001f)
            return true;

        Vector2i sourceNeighbor;
        if (Math.Abs(directionToSource.X) > Math.Abs(directionToSource.Y))
        {
            sourceNeighbor = new Vector2i(Math.Sign(directionToSource.X), 0);
        }
        else
        {
            sourceNeighbor = new Vector2i(0, Math.Sign(directionToSource.Y));
        }

        return !IsOpeningTile(grid, tile + sourceNeighbor, map, tileDefinition);
    }

    public static bool IsOpeningPerimeterTile(
        Entity<MapGridComponent> grid,
        Vector2i tile,
        SharedMapSystem map,
        ITileDefinitionManager tileDefinition)
    {
        return !IsOpeningTile(grid, tile + new Vector2i(1, 0), map, tileDefinition) ||
               !IsOpeningTile(grid, tile + new Vector2i(-1, 0), map, tileDefinition) ||
               !IsOpeningTile(grid, tile + new Vector2i(0, 1), map, tileDefinition) ||
               !IsOpeningTile(grid, tile + new Vector2i(0, -1), map, tileDefinition);
    }

    private CachedChunk GetChunkOpenings(
        Entity<MapGridComponent> grid,
        Vector2i chunk,
        SharedMapSystem map,
        ITileDefinitionManager tile)
    {
        if (!_gridCaches.TryGetValue(grid.Owner, out var cache))
        {
            cache = new GridOpeningCache();
            _gridCaches[grid.Owner] = cache;
        }

        if (cache.LastTileModifiedTick != grid.Comp.LastTileModifiedTick)
        {
            cache.LastTileModifiedTick = grid.Comp.LastTileModifiedTick;
            cache.Chunks.Clear();
        }

        if (cache.Chunks.TryGetValue(chunk, out var cached))
            return cached;

        cached = CalculateChunkOpenings(grid, chunk, map, tile);
        cache.Chunks[chunk] = cached;
        return cached;
    }

    private bool ForEachOpeningTileInBounds(
        Entity<MapGridComponent> grid,
        Vector2i chunk,
        int startX,
        int endX,
        int startY,
        int endY,
        SharedMapSystem map,
        ITileDefinitionManager tileDefinition,
        Func<Vector2i, bool> visitor)
    {
        var cached = GetChunkOpenings(grid, chunk, map, tileDefinition);
        if (!cached.HasOpening)
            return false;

        if (chunkSize == DefaultChunkSize)
        {
            var chunkStart = chunk * DefaultChunkSize;
            var tileStartX = Math.Max(startX, chunkStart.X);
            var tileEndX = Math.Min(endX, chunkStart.X + DefaultChunkSize - 1);
            var tileStartY = Math.Max(startY, chunkStart.Y);
            var tileEndY = Math.Min(endY, chunkStart.Y + DefaultChunkSize - 1);

            for (var tileY = tileStartY; tileY <= tileEndY; tileY++)
            {
                for (var tileX = tileStartX; tileX <= tileEndX; tileX++)
                {
                    var bit = OpeningMaskBit(chunkStart, tileX, tileY);
                    if ((cached.OpeningMask & bit) == 0)
                        continue;

                    if (visitor(new Vector2i(tileX, tileY)))
                        return true;
                }
            }

            return false;
        }

        var fallbackChunkStart = chunk * chunkSize;
        var fallbackTileStartX = Math.Max(startX, fallbackChunkStart.X);
        var fallbackTileEndX = Math.Min(endX, fallbackChunkStart.X + chunkSize - 1);
        var fallbackTileStartY = Math.Max(startY, fallbackChunkStart.Y);
        var fallbackTileEndY = Math.Min(endY, fallbackChunkStart.Y + chunkSize - 1);

        for (var tileX = fallbackTileStartX; tileX <= fallbackTileEndX; tileX++)
        {
            for (var tileY = fallbackTileStartY; tileY <= fallbackTileEndY; tileY++)
            {
                var openingTile = new Vector2i(tileX, tileY);
                if (!IsOpeningTile(grid, openingTile, map, tileDefinition))
                    continue;

                if (visitor(openingTile))
                    return true;
            }
        }

        return false;
    }

    private void TryFindNearestOpeningCenterInChunk(
        Entity<MapGridComponent> grid,
        Vector2i chunk,
        int startX,
        int endX,
        int startY,
        int endY,
        Vector2 sourcePosition,
        Vector2 localSourcePosition,
        bool sourceInsideOpening,
        Matrix3x2 gridWorldMatrix,
        float searchRadiusSquared,
        bool edgeOnly,
        SharedMapSystem map,
        ITileDefinitionManager tileDefinition,
        ref bool foundOpening,
        ref float bestDistanceSquared,
        ref Vector2 bestOpeningCenter)
    {
        var cached = GetChunkOpenings(grid, chunk, map, tileDefinition);
        if (!cached.HasOpening)
            return;

        if (chunkSize == DefaultChunkSize)
        {
            var chunkStart = chunk * DefaultChunkSize;
            var tileStartX = Math.Max(startX, chunkStart.X);
            var tileEndX = Math.Min(endX, chunkStart.X + DefaultChunkSize - 1);
            var tileStartY = Math.Max(startY, chunkStart.Y);
            var tileEndY = Math.Min(endY, chunkStart.Y + DefaultChunkSize - 1);

            for (var tileY = tileStartY; tileY <= tileEndY; tileY++)
            {
                for (var tileX = tileStartX; tileX <= tileEndX; tileX++)
                {
                    var bit = OpeningMaskBit(chunkStart, tileX, tileY);
                    if ((cached.OpeningMask & bit) == 0)
                        continue;

                    TryUseNearestOpeningTile(
                        grid,
                        new Vector2i(tileX, tileY),
                        sourcePosition,
                        localSourcePosition,
                        sourceInsideOpening,
                        gridWorldMatrix,
                        searchRadiusSquared,
                        edgeOnly,
                        map,
                        tileDefinition,
                        ref foundOpening,
                        ref bestDistanceSquared,
                        ref bestOpeningCenter);
                }
            }

            return;
        }

        var fallbackChunkStart = chunk * chunkSize;
        var fallbackTileStartX = Math.Max(startX, fallbackChunkStart.X);
        var fallbackTileEndX = Math.Min(endX, fallbackChunkStart.X + chunkSize - 1);
        var fallbackTileStartY = Math.Max(startY, fallbackChunkStart.Y);
        var fallbackTileEndY = Math.Min(endY, fallbackChunkStart.Y + chunkSize - 1);

        for (var tileX = fallbackTileStartX; tileX <= fallbackTileEndX; tileX++)
        {
            for (var tileY = fallbackTileStartY; tileY <= fallbackTileEndY; tileY++)
            {
                var openingTile = new Vector2i(tileX, tileY);
                if (!IsOpeningTile(grid, openingTile, map, tileDefinition))
                    continue;

                TryUseNearestOpeningTile(
                    grid,
                    openingTile,
                    sourcePosition,
                    localSourcePosition,
                    sourceInsideOpening,
                    gridWorldMatrix,
                    searchRadiusSquared,
                    edgeOnly,
                    map,
                    tileDefinition,
                    ref foundOpening,
                    ref bestDistanceSquared,
                    ref bestOpeningCenter);
            }
        }
    }

    private static void TryUseNearestOpeningTile(
        Entity<MapGridComponent> grid,
        Vector2i openingTile,
        Vector2 sourcePosition,
        Vector2 localSourcePosition,
        bool sourceInsideOpening,
        Matrix3x2 gridWorldMatrix,
        float searchRadiusSquared,
        bool edgeOnly,
        SharedMapSystem map,
        ITileDefinitionManager tileDefinition,
        ref bool foundOpening,
        ref float bestDistanceSquared,
        ref Vector2 bestOpeningCenter)
    {
        if (edgeOnly &&
            !IsOpeningEdgeTile(grid, openingTile, localSourcePosition, sourceInsideOpening, map, tileDefinition))
        {
            return;
        }

        var center = Vector2.Transform(
            new Vector2(openingTile.X + 0.5f, openingTile.Y + 0.5f),
            gridWorldMatrix);
        var distanceSquared = Vector2.DistanceSquared(sourcePosition, center);
        if (distanceSquared > searchRadiusSquared ||
            distanceSquared >= bestDistanceSquared)
        {
            return;
        }

        foundOpening = true;
        bestDistanceSquared = distanceSquared;
        bestOpeningCenter = center;
    }

    private CachedChunk CalculateChunkOpenings(
        Entity<MapGridComponent> grid,
        Vector2i chunk,
        SharedMapSystem map,
        ITileDefinitionManager tile)
    {
        var startX = chunk.X * chunkSize;
        var startY = chunk.Y * chunkSize;
        var endX = startX + chunkSize;
        var endY = startY + chunkSize;

        var hasOpening = false;
        var openingMask = 0UL;

        for (var x = startX; x < endX; x++)
        {
            for (var y = startY; y < endY; y++)
            {
                if (IsOpeningTile(grid, new Vector2i(x, y), map, tile))
                {
                    hasOpening = true;

                    if (chunkSize == DefaultChunkSize)
                        openingMask |= OpeningMaskBit(new Vector2i(startX, startY), x, y);
                }
            }
        }

        return new CachedChunk(hasOpening, openingMask);
    }

    private static ulong OpeningMaskBit(Vector2i chunkStart, int tileX, int tileY)
    {
        var localX = tileX - chunkStart.X;
        var localY = tileY - chunkStart.Y;
        var bit = localY * DefaultChunkSize + localX;
        return 1UL << bit;
    }

    private static void GetTileSearchBounds(
        Entity<MapGridComponent> grid,
        MapCoordinates bottomLeft,
        MapCoordinates topRight,
        SharedMapSystem map,
        out int startX,
        out int endX,
        out int startY,
        out int endY)
    {
        var tileBottomLeft = map.TileIndicesFor(grid.Owner, grid.Comp, bottomLeft);
        var tileTopRight = map.TileIndicesFor(grid.Owner, grid.Comp, topRight);

        startX = Math.Min(tileBottomLeft.X, tileTopRight.X) - 1;
        endX = Math.Max(tileBottomLeft.X, tileTopRight.X) + 1;
        startY = Math.Min(tileBottomLeft.Y, tileTopRight.Y) - 1;
        endY = Math.Max(tileBottomLeft.Y, tileTopRight.Y) + 1;
    }

    private static void AddOpeningBounds(
        List<Box2> openingBounds,
        Box2 bounds,
        ref Box2 combinedOpeningBounds,
        ref bool foundOpening)
    {
        openingBounds.Add(bounds);
        combinedOpeningBounds = foundOpening
            ? combinedOpeningBounds.Union(bounds)
            : bounds;
        foundOpening = true;
    }

    private sealed class GridOpeningCache
    {
        public GameTick LastTileModifiedTick;
        public readonly Dictionary<Vector2i, CachedChunk> Chunks = new();
    }

    private readonly record struct CachedChunk(bool HasOpening, ulong OpeningMask);
}
