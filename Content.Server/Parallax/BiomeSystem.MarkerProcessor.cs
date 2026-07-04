// Mono - Refactored into smaller subsystems
using System.Threading.Tasks;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Markers;
using Robust.Shared.Collections;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Parallax;

public sealed partial class BiomeSystem
{
    private void InitializeMarkerProcessor()
    {
        // MarkerProcessor methods are now part of this partial class
    }

    /// <summary>
    /// Goes through all marker chunks that haven't been calculated, then calculates what spawns there are and
    /// allocates them to the relevant actual chunks in the biome (marker chunks may be many times larger than biome chunks).
    /// </summary>
    private void BuildMarkerChunks(BiomeComponent component, EntityUid gridUid, MapGridComponent grid, int seed)
    {
        var markers = _markerChunks[component];
        var loadedMarkers = component.LoadedMarkers;
        var idx = 0;

        foreach (var (layer, chunks) in markers)
        {
            // I know dictionary ordering isn't guaranteed but I just need something to differentiate seeds.
            idx++;
            var localIdx = idx;

            Parallel.ForEach(chunks, new ParallelOptions() { MaxDegreeOfParallelism = _parallel.ParallelProcessCount }, chunk =>
            {
                if (loadedMarkers.TryGetValue(layer, out var mobChunks) && mobChunks.Contains(chunk))
                    return;

                var forced = component.ForcedMarkerLayers.Contains(layer);

                // Make a temporary version and copy back in later.
                var pending = new Dictionary<Vector2i, Dictionary<string, List<Vector2i>>>();

                // Essentially get the seed + work out a buffer to adjacent chunks so we don't
                // inadvertantly spawn too many near the edges.
                var layerProto = ProtoManager.Index<BiomeMarkerLayerPrototype>(layer);
                var markerSeed = seed + chunk.X * ChunkSize + chunk.Y + localIdx;
                var rand = new Random(markerSeed);
                var buffer = (int)(layerProto.Radius / 2f);
                var bounds = new Box2i(chunk + buffer, chunk + layerProto.Size - buffer);
                var count = (int)(bounds.Area / (layerProto.Radius * layerProto.Radius));
                count = Math.Min(count, layerProto.MaxCount);

                GetMarkerNodes(gridUid, component, grid, layerProto, forced, bounds, count, rand,
                    out var spawnSet, out var existing);

                // Forcing markers to spawn so delete any that were found to be in the way.
                if (forced && existing.Count > 0)
                {
                    // Lock something so we can delete these safely.
                    lock (component.PendingMarkers)
                    {
                        foreach (var ent in existing)
                        {
                            Del(ent);
                        }
                    }
                }

                foreach (var node in spawnSet.Keys)
                {
                    var chunkOrigin = SharedMapSystem.GetChunkIndices(node, ChunkSize) * ChunkSize;

                    if (!pending.TryGetValue(chunkOrigin, out var pendingMarkers))
                    {
                        pendingMarkers = new Dictionary<string, List<Vector2i>>();
                        pending[chunkOrigin] = pendingMarkers;
                    }

                    if (!pendingMarkers.TryGetValue(layer, out var layerMarkers))
                    {
                        layerMarkers = new List<Vector2i>();
                        pendingMarkers[layer] = layerMarkers;
                    }

                    layerMarkers.Add(node);
                }

                lock (loadedMarkers)
                {
                    if (!loadedMarkers.TryGetValue(layer, out var lockMobChunks))
                    {
                        lockMobChunks = new HashSet<Vector2i>();
                        loadedMarkers[layer] = lockMobChunks;
                    }

                    lockMobChunks.Add(chunk);

                    foreach (var (chunkOrigin, layers) in pending)
                    {
                        if (!component.PendingMarkers.TryGetValue(chunkOrigin, out var lockMarkers))
                        {
                            lockMarkers = new Dictionary<string, List<Vector2i>>();
                            component.PendingMarkers[chunkOrigin] = lockMarkers;
                        }

                        foreach (var (lockLayer, nodes) in layers)
                        {
                            lockMarkers[lockLayer] = nodes;
                        }
                    }
                }
            });
        }

        component.ForcedMarkerLayers.Clear();
    }

    /// <summary>
    /// Gets the marker nodes for the specified area.
    /// </summary>
    /// <param name="emptyTiles">Should we include empty tiles when determine markers (e.g. if they are yet to be loaded)</param>
    public void GetMarkerNodes(
        EntityUid gridUid,
        BiomeComponent biome,
        MapGridComponent grid,
        BiomeMarkerLayerPrototype layerProto,
        bool forced,
        Box2i bounds,
        int count,
        Random rand,
        out Dictionary<Vector2i, string?> spawnSet,
        out HashSet<EntityUid> existingEnts,
        bool emptyTiles = true)
    {
        DebugTools.Assert(count > 0);
        var remainingTiles = _tilePool.Get();
        var nodeEntities = new Dictionary<Vector2i, EntityUid?>();
        var nodeMask = new Dictionary<Vector2i, string?>();

        // Okay so originally we picked a random tile and BFS outwards
        // the problem is if you somehow get a cooked frontier then it might drop entire veins
        // hence we'll grab all valid tiles up front and use that as possible seeds.
        // It's hella more expensive but stops issues.
        for (var x = bounds.Left; x < bounds.Right; x++)
        {
            for (var y = bounds.Bottom; y < bounds.Top; y++)
            {
                var node = new Vector2i(x, y);

                // Empty tile, skip if relevant.
                if (!emptyTiles && (!_mapSystem.TryGetTile(grid, node, out var tile) || tile.IsEmpty))
                    continue;

                // Check if it's a valid spawn, if so then use it.
                var enumerator = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, node);
                enumerator.MoveNext(out var existing);

                if (!forced && existing != null)
                    continue;

                // Check if mask matches // anything blocking.
                TryGetEntity(node, biome, (gridUid, grid), out var proto);

                // If there's an existing entity and it doesn't match the mask then skip.
                if (layerProto.EntityMask.Count > 0 &&
                    (proto == null ||
                     !layerProto.EntityMask.ContainsKey(proto)))
                {
                    continue;
                }

                // If it's just a flat spawn then just check for anything blocking.
                if (proto != null && layerProto.Prototype != null)
                {
                    continue;
                }

                DebugTools.Assert(layerProto.EntityMask.Count == 0 || !string.IsNullOrEmpty(proto));
                remainingTiles.Add(node);
                nodeEntities.Add(node, existing);
                nodeMask.Add(node, proto);
            }
        }

        var frontier = new ValueList<Vector2i>(32);
        // TODO: Need poisson but crashes whenever I use moony's due to inputs or smth idk
        // Get the total amount of groups to spawn across the entire chunk.
        // We treat a null entity mask as requiring nothing else on the tile

        spawnSet = new Dictionary<Vector2i, string?>();
        existingEnts = new HashSet<EntityUid>();

        // Iterate the group counts and pathfind out each group.
        for (var i = 0; i < count; i++)
        {
            var groupSize = rand.Next(layerProto.MinGroupSize, layerProto.MaxGroupSize + 1);

            // While we have remaining tiles keep iterating
            while (groupSize > 0 && remainingTiles.Count > 0)
            {
                var startNode = rand.PickAndTake(remainingTiles);
                frontier.Clear();
                frontier.Add(startNode);

                // This essentially may lead to a vein being split in multiple areas but the count matters more than position.
                while (frontier.Count > 0 && groupSize > 0)
                {
                    // Need to pick a random index so we don't just get straight lines of ores.
                    var frontierIndex = rand.Next(frontier.Count);
                    var node = frontier[frontierIndex];
                    frontier.RemoveSwap(frontierIndex);
                    remainingTiles.Remove(node);

                    // Add neighbors if they're valid, worst case we add no more and pick another random seed tile.
                    for (var x = -1; x <= 1; x++)
                    {
                        for (var y = -1; y <= 1; y++)
                        {
                            var neighbor = new Vector2i(node.X + x, node.Y + y);

                            if (frontier.Contains(neighbor) || !remainingTiles.Contains(neighbor))
                                continue;

                            frontier.Add(neighbor);
                        }
                    }

                    // Tile valid salad so add it.
                    var mask = nodeMask[node];
                    spawnSet.Add(node, mask);
                    groupSize--;

                    if (nodeEntities.TryGetValue(node, out var existing))
                    {
                        Del(existing);
                    }
                }
            }

            if (groupSize > 0)
            {
                Log.Warning($"Found remaining group size for ore veins!");
            }
        }

        _tilePool.Return(remainingTiles);
    }

    /// <summary>
    /// Loads the pre-deteremined marker nodes for a particular chunk.
    /// This is calculated in <see cref="BuildMarkerChunks"/>
    /// </summary>
    /// <remarks>
    /// Note that the marker chunks do not correspond to this chunk.
    /// </remarks>
    private void LoadChunkMarkers(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        int seed)
    {
        // Load any pending marker tiles first.
        if (!component.PendingMarkers.TryGetValue(chunk, out var layers))
            return;

        // This needs to be done separately in case we try to add a marker layer and want to force it on existing
        // loaded chunks.
        component.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= _tilePool.Get();

        foreach (var (layer, nodes) in layers)
        {
            var layerProto = ProtoManager.Index<BiomeMarkerLayerPrototype>(layer);

            foreach (var node in nodes)
            {
                if (modified.Contains(node))
                    continue;

                // Need to ensure the tile under it has loaded for anchoring.
                if (TryGetBiomeTile(node, component.Layers, seed, (gridUid, grid), out var tile))
                {
                    _mapSystem.SetTile(gridUid, grid, node, tile.Value);
                }

                string? prototype;

                if (TryGetEntity(node, component, (gridUid, grid), out var proto) &&
                    layerProto.EntityMask.TryGetValue(proto, out var maskedProto))
                {
                    prototype = maskedProto;
                }
                else
                {
                    prototype = layerProto.Prototype;
                }

                // If it is a ghost role then purge it
                // TODO: This is *kind* of a bandaid but natural mobs spawns needs a lot more work.
                // Ideally we'd just have ghost role and non-ghost role variants for some stuff.
                var uid = EntityManager.CreateEntityUninitialized(prototype, _mapSystem.GridTileToLocal(gridUid, grid, node));
                RemComp<GhostTakeoverAvailableComponent>(uid);
                RemComp<GhostRoleComponent>(uid);
                EntityManager.InitializeAndStartEntity(uid);
                modified.Add(node);
            }
        }

        if (modified.Count == 0)
        {
            component.ModifiedTiles.Remove(chunk);
            _tilePool.Return(modified);
        }

        component.PendingMarkers.Remove(chunk);
    }
}
