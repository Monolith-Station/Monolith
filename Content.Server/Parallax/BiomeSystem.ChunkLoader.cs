// Mono - Refactored into smaller subsystems
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Linq;


namespace Content.Server.Parallax;

public sealed partial class BiomeSystem
{
    private readonly List<(Vector2i, Tile)> _chunkLoaderTiles = new();
    private readonly List<(EntityUid, Vector2i)> _chunkLoaderEntities = new();
    private readonly List<(string, EntityCoordinates)> _chunkLoaderDecals = new();
    private readonly List<EntityUid> _chunkLoaderEntitiesToDelete = new();
    private readonly List<uint> _chunkLoaderDecalsToDelete = new();

    // Pre-size data structures because malloc is still real
    private void InitializeChunkLoader()
    {
        var expectedChunkSize = ChunkSize * ChunkSize;
        _chunkLoaderTiles.Capacity = expectedChunkSize;
        _chunkLoaderEntities.Capacity = expectedChunkSize / 4;
        _chunkLoaderDecals.Capacity = expectedChunkSize / 8;
        _chunkLoaderEntitiesToDelete.Capacity = expectedChunkSize / 4;
        _chunkLoaderDecalsToDelete.Capacity = expectedChunkSize / 8;
    }
    private void ForEachTileInChunk(Vector2i chunk, HashSet<Vector2i> modified, Action<Vector2i> action)
    {
        var startX = chunk.X;
        var startY = chunk.Y;
        var endX = startX + ChunkSize;
        var endY = startY + ChunkSize;

        for (var x = startX; x < endX; x++)
        {
            for (var y = startY; y < endY; y++)
            {
                var indices = new Vector2i(x, y);
                if (!modified.Contains(indices))
                    action(indices);
            }
        }
    }

    private bool HasAnchoredEntity(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);
        return anchored.MoveNext(out _);
    }

    /// <summary>
    /// Loads a particular queued chunk for a biome.
    /// </summary>
    private void LoadChunk(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        int seed)
    {
        component.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= _tilePool.Get();
        _chunkLoaderTiles.Clear();

        LoadTiles(component, gridUid, grid, chunk, seed, modified);
        LoadEntities(component, gridUid, grid, chunk, seed, modified);
        LoadDecals(component, gridUid, grid, chunk, seed, modified);

        FinalizeChunk(component, chunk, modified);
    }

    private void LoadTiles(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        int seed,
        HashSet<Vector2i> modified)
    {
        _chunkLoaderTiles.Clear();

        ForEachTileInChunk(chunk, modified, indices =>
        {
            if (_mapSystem.TryGetTileRef(gridUid, grid, indices, out var tileRef) && !tileRef.Tile.IsEmpty)
                return;

            if (TryGetBiomeTile(indices, component.Layers, seed, (gridUid, grid), out var biomeTile))
            {
                _chunkLoaderTiles.Add((indices, biomeTile.Value));
            }
        });

        if (_chunkLoaderTiles.Count > 0) // Don't need to call setTiles if there's nothing to load
        {
            _mapSystem.SetTiles(gridUid, grid, _chunkLoaderTiles);
        }
    }

    private void LoadEntities(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        int seed,
        HashSet<Vector2i> modified)
    {
        var loadedEntities = new Dictionary<EntityUid, Vector2i>();
        component.LoadedEntities.Add(chunk, loadedEntities);

        ForEachTileInChunk(chunk, modified, indices =>
        {
            if (HasAnchoredEntity(gridUid, grid, indices))
                return;

            if (TryGetEntity(indices, component, (gridUid, grid), out var entPrototype))
            {
                var ent = Spawn(entPrototype, _mapSystem.GridTileToLocal(gridUid, grid, indices));

                if (_xformQuery.TryGetComponent(ent, out var xform) && !xform.Anchored)
                {
                    _transform.AnchorEntity((ent, xform), (gridUid, grid), indices);
                }

                loadedEntities.Add(ent, indices);
            }
        });
    }

    private void LoadDecals(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        int seed,
        HashSet<Vector2i> modified)
    {
        var loadedDecals = new Dictionary<uint, Vector2i>();
        component.LoadedDecals.Add(chunk, loadedDecals);

        _chunkLoaderDecals.Clear();

        ForEachTileInChunk(chunk, modified, indices =>
        {
            if (HasAnchoredEntity(gridUid, grid, indices) || !TryGetDecals(indices, component.Layers, seed, (gridUid, grid), out var decals))
                return;

            foreach (var decal in decals)
            {
                _chunkLoaderDecals.Add((decal.ID, new EntityCoordinates(gridUid, decal.Position)));
            }
        });

        // Batch create
        foreach (var (decalId, coords) in _chunkLoaderDecals)
        {
            if (_decals.TryAddDecal(decalId, coords, out var dec))
            {
                var tilePos = _mapSystem.LocalToTile(gridUid, grid, coords);
                loadedDecals.Add(dec, tilePos);
            }
        }
    }

    private void FinalizeChunk(BiomeComponent component, Vector2i chunk, HashSet<Vector2i> modified)
    {
        if (modified.Count == 0)
        {
            _tilePool.Return(modified);
            component.ModifiedTiles.Remove(chunk);
        }
        else
        {
            component.ModifiedTiles[chunk] = modified;
        }
    }

    /// <summary>
    /// Unloads a specific biome chunk.
    /// </summary>
    private void UnloadChunk(BiomeComponent component, EntityUid gridUid, MapGridComponent grid, Vector2i chunk, int seed, List<(Vector2i, Tile)> tiles)
    {
        component.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= new HashSet<Vector2i>();

        UnloadDecals(component, gridUid, chunk, modified);
        UnloadEntities(component, gridUid, grid, chunk, modified);
        UnloadTiles(component, gridUid, grid, chunk, seed, modified, tiles);

        component.LoadedChunks.Remove(chunk);

        if (modified.Count == 0)
        {
            component.ModifiedTiles.Remove(chunk);
        }
        else
        {
            component.ModifiedTiles[chunk] = modified;
        }
    }

    private void UnloadDecals(BiomeComponent component, EntityUid gridUid, Vector2i chunk, HashSet<Vector2i> modified)
    {
        if (!component.LoadedDecals.TryGetValue(chunk, out var loadedDecals))
            return;

        _chunkLoaderDecalsToDelete.Clear();

        // Batch collect decals to delete
        foreach (var (dec, indices) in loadedDecals)
        {
            _chunkLoaderDecalsToDelete.Add(dec);
        }

        // Batch process decal removal
        foreach (var dec in _chunkLoaderDecalsToDelete)
        {
            if (!_decals.RemoveDecal(gridUid, dec))
            {
                // Find the indices for this decal to mark as modified
                foreach (var (decalId, indices) in loadedDecals)
                {
                    if (decalId == dec)
                    {
                        modified.Add(indices);
                        break;
                    }
                }
            }
        }
        component.LoadedDecals.Remove(chunk);
    }

    private void UnloadEntities(BiomeComponent component, EntityUid gridUid, MapGridComponent grid, Vector2i chunk, HashSet<Vector2i> modified)
    {
        if (!component.LoadedEntities.TryGetValue(chunk, out var loadedEntities))
            return;

        _chunkLoaderEntitiesToDelete.Clear();
        var xformQuery = GetEntityQuery<TransformComponent>();

        // Batch validate entities and collect ones to delete
        foreach (var (ent, tile) in loadedEntities)
        {
            if (Deleted(ent) || !xformQuery.TryGetComponent(ent, out var xform))
            {
                modified.Add(tile);
                continue;
            }

            var entTile = _mapSystem.LocalToTile(gridUid, grid, xform.Coordinates);

            if (!xform.Anchored || entTile != tile)
            {
                modified.Add(tile);
                continue;
            }

            if (!EntityManager.IsDefault(ent))
            {
                modified.Add(tile);
                continue;
            }

            _chunkLoaderEntitiesToDelete.Add(ent);
        }

        // Batch delete entities
        foreach (var ent in _chunkLoaderEntitiesToDelete)
        {
            Del(ent);
        }

        component.LoadedEntities.Remove(chunk);
    }

    private void UnloadTiles(BiomeComponent component, EntityUid gridUid, MapGridComponent grid, Vector2i chunk, int seed, HashSet<Vector2i> modified, List<(Vector2i, Tile)> tiles)
    {
        ForEachTileInChunk(chunk, modified, indices =>
        {
            if (HasAnchoredEntity(gridUid, grid, indices))
            {
                modified.Add(indices);
                return;
            }

            if (!TryGetBiomeTile(indices, component.Layers, seed, null, out var biomeTile) ||
                _mapSystem.TryGetTileRef(gridUid, grid, indices, out var tileRef) && tileRef.Tile != biomeTile.Value)
            {
                modified.Add(indices);
                return;
            }

            tiles.Add((indices, Tile.Empty));
        });

        // Batch tile removal
        if (tiles.Count > 0)
        {
            _mapSystem.SetTiles(gridUid, grid, tiles);
            tiles.Clear();
        }
    }

    /// <summary>
    /// Handles all of the queued chunk unloads for a particular biome.
    /// </summary>
    private void UnloadChunks(BiomeComponent component, EntityUid gridUid, MapGridComponent grid, int seed)
    {
        var active = _activeChunks[component];

        var loadedChunksList = component.LoadedChunks.ToList();
        for (int i = loadedChunksList.Count - 1; i >= 0; i--)
        {
            var chunk = loadedChunksList[i];
            if (active.Contains(chunk))
                continue;

            var tiles = new List<(Vector2i, Tile)>(ChunkSize * ChunkSize);
            UnloadChunk(component, gridUid, grid, chunk, seed, tiles);
            return;
        }
    }
}
