using Content.Server.Procedural;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server.Parallax;

public sealed partial class BiomeSystem
{
    [Dependency] private DungeonSystem _dungeons = default!;

    private void LoadPointsOfInterest(BiomeComponent biome, EntityUid uid, MapGridComponent grid)
    {
        if (biome.PointOfInterestRooms.Count == 0)
            return;

        var spacing = biome.PointOfInterestSpacing;
        if (spacing < ChunkSize)
            return;

        foreach (var chunk in _activeChunks[biome])
        {
            var region = SharedMapSystem.GetChunkIndices(chunk, spacing);
            if (!biome.ProcessedPointOfInterestRegions.Add(region))
                continue;

            var seed = unchecked((biome.Seed * 73856093) ^ (region.X * 19349663) ^ (region.Y * 83492791));
            var random = new Random(seed);
            var room = _proto.Index(biome.PointOfInterestRooms[random.Next(biome.PointOfInterestRooms.Count)]);
            if (room.Size.X + 16 >= spacing || room.Size.Y + 16 >= spacing)
                continue;

            for (var attempt = 0; attempt < 8; attempt++)
            {
                var origin = region * spacing + new Vector2i(
                    random.Next(8, spacing - room.Size.X - 8),
                    random.Next(8, spacing - room.Size.Y - 8));
                var bounds = new Box2i(origin - Vector2i.One, origin + room.Size + Vector2i.One);
                if (!CanPlacePointOfInterest(uid, grid, biome, bounds))
                    continue;

                var tiles = new List<(Vector2i, Tile)>();
                ReserveTiles(uid, (Box2) bounds, tiles, biome, grid);
                _dungeons.SpawnRoom(uid, grid, origin, room, random, null);
                break;
            }
        }
    }

    private bool CanPlacePointOfInterest(EntityUid uid, MapGridComponent grid, BiomeComponent biome,
        Box2i bounds)
    {
        for (var x = bounds.Left; x < bounds.Right; x++)
        for (var y = bounds.Bottom; y < bounds.Top; y++)
        {
            var index = new Vector2i(x, y);
            var chunk = SharedMapSystem.GetChunkIndices(index, ChunkSize) * ChunkSize;
            if (biome.ModifiedTiles.TryGetValue(chunk, out var modified) && modified.Contains(index) ||
                HasAnchoredEntity(uid, grid, index))
                return false;

            if (!TryGetTile(index, biome.Layers, biome.Seed, null, out var tile) ||
                !biome.PointOfInterestTiles.Contains(TileDefManager[tile.Value.TypeId].ID))
                return false;

            if (_mapSystem.TryGetTileRef(uid, grid, index, out var existing) &&
                !existing.Tile.IsEmpty && existing.Tile != tile.Value)
                return false;
        }

        return true;
    }
}
