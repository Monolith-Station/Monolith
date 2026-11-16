using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Mono.PlanetMap;

// This is for structures built directly on the planet's own grid.
// Yes, I am also suprised building on the planet isn't a seperate grid. So this is kindof janky.
// We're compare each tile against the seeded biome tiles at their position. Ground that the server loaded from
// the biome will match the seed and anything placed on top of it like floors and plating wont. The mismatch
// is what we outline, and pretend it's a grid.
public sealed partial class PlanetTerrainSystem
{
    [Dependency] private SharedMapSystem _mapSystem = default!;

    /// <summary>
    /// Regions per worker job
    /// </summary>
    private const int StructureRegionsPerJob = 32;

    /// <summary>
    /// Side length of our structure regions in tiles. Wholey Independent of the grid's own chunk size,
    /// which is internal to the engine apparently.
    /// </summary>
    private const int StructureRegionSize = 16;

    /// <summary>
    /// Tiles copied per frame during the planets first scan.
    /// </summary>
    private const int ScanTilesPerFrame = 8192;

    /// <summary>
    /// If the grid's chunk layout keeps changing or messing up mid scan, just give up and send it.
    /// </summary>
    private const int MaxScanRestarts = 3;

    private readonly ConcurrentQueue<StructureResult> _structureResults = new();

    /// <summary>
    /// Jobs waiting for a structure worker. Main thread only.
    /// </summary>
    private readonly Queue<StructureJob> _structureQueue = new();

    /// <summary>
    /// Running structure jobs. Separate from the terrain chunk counter so neither killes eachother.
    /// </summary>
    private int _structureRunning;

    /// <summary>
    /// Structure scanning is background work, so it only gets about half as many workers as terrain
    /// </summary>
    private readonly int _maxStructureWorkers = Math.Clamp(Environment.ProcessorCount / 4, 1, 2);
    private readonly List<StructureSnapshot> _snapshotScratch = new();
    private readonly List<Vector2i> _dirtyScratch = new();
    private readonly HashSet<Vector2i> _rebuildScratch = new();

    private void InitializeStructures()
    {
        // Broadcast subscription: MapGridComponent + TileChangedEvent is already taken by another client system.
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
    }

    private void OnTileChanged(ref TileChangedEvent ev)
    {
        if (!_caches.TryGetValue(ev.Entity.Owner, out var cache))
            return;

        // Also recorded mid-scan, so changes to regions the scan already passed aren't missed.
        var dirty = cache.Structures.Dirty;
        foreach (var change in ev.Changes)
        {
            dirty.Add(SharedMapSystem.GetChunkIndices(change.GridIndices, StructureRegionSize));
        }
    }

    /// <summary>
    /// Draws tiles built on the planet grid with the tinted fill and outline, pretending it's a seperate grid.
    /// </summary>
    public void DrawStructures(DrawingHandleScreen handle, TerrainCache cache, Box2 viewBox, Matrix3x2 worldToView, Color color, Color backingColor)
    {
        var layer = cache.Structures;
        layer.LastViewed = _frame;

        if (layer.Geometry.Count == 0)
            return;

        // The map grid IS the map, so local == world, but dont assume it.
        var gridToView = _transform.GetWorldMatrix(cache.MapUid) * worldToView;
        var fillCount = 0;
        var edgeCount = 0;

        foreach (var geometry in layer.Geometry.Values)
        {
            if (!geometry.Bounds.Intersects(viewBox))
                continue;

            EnsureCapacity(ref _structureFill, fillCount + geometry.Fill.Length);
            EnsureCapacity(ref _structureEdges, edgeCount + geometry.Edges.Length);

            foreach (var v in geometry.Fill)
            {
                _structureFill[fillCount++] = Vector2.Transform(v, gridToView);
            }

            foreach (var v in geometry.Edges)
            {
                _structureEdges[edgeCount++] = Vector2.Transform(v, gridToView);
            }
        }

        var fillColor = Color.ToSrgb(Color.InterpolateBetween(backingColor, color, 0.15f)).WithAlpha(1f);
        const int triBatch = 3 * 4096;
        const int lineBatch = 2 * 4096;

        for (var start = 0; start < fillCount; start += triBatch)
        {
            var count = Math.Min(triBatch, fillCount - start);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, new Span<Vector2>(_structureFill, start, count), fillColor);
        }

        for (var start = 0; start < edgeCount; start += lineBatch)
        {
            var count = Math.Min(lineBatch, edgeCount - start);
            handle.DrawPrimitives(DrawPrimitiveTopology.LineList, new Span<Vector2>(_structureEdges, start, count), color);
        }
    }

    private Vector2[] _structureFill = new Vector2[256];
    private Vector2[] _structureEdges = new Vector2[256];

    private static void EnsureCapacity(ref Vector2[] array, int needed)
    {
        if (array.Length >= needed)
            return;

        var size = array.Length;
        while (size < needed)
        {
            size *= 2;
        }

        Array.Resize(ref array, size);
    }

    private void DispatchStructureWork()
    {
        foreach (var cache in _caches.Values)
        {
            var layer = cache.Structures;

            // Only keep structures up to date for planets someone is looking at.
            if (layer.LastViewed < _frame - 2)
                continue;

            if (!TryComp<MapGridComponent>(cache.MapUid, out var grid))
                continue;

            if (!layer.Scanned)
            {
                ContinueScan(cache, grid);
                continue;
            }

            if (layer.Dirty.Count == 0)
                continue;

            _snapshotScratch.Clear();
            _dirtyScratch.Clear();
            _dirtyScratch.AddRange(layer.Dirty);
            layer.Dirty.Clear();

            foreach (var region in _dirtyScratch)
            {
                _snapshotScratch.Add(SnapshotRegion(cache, grid, layer, region));
            }

            QueueStructureJobs(cache);
        }
    }

    /// <summary>
    /// Copies up to <see cref="ScanTilesPerFrame"/> tiles of the planet grid, finishing the scan over several frames to not kill our chromebook users.
    /// </summary>
    private void ContinueScan(TerrainCache cache, MapGridComponent grid)
    {
        var layer = cache.Structures;
        const int size = StructureRegionSize;

        layer.ScanRator ??= _mapSystem.GetAllTilesEnumerator(cache.MapUid, grid);

        // After repeated restarts, stop slicing so the scan is guaranteed to finish.
        var budget = layer.ScanRestarts >= MaxScanRestarts ? int.MaxValue : ScanTilesPerFrame;
        var rator = layer.ScanRator.Value;
        var finished = false;

        try
        {
            for (var i = 0; i < budget; i++)
            {
                if (!rator.MoveNext(out var tileRef))
                {
                    finished = true;
                    break;
                }

                var indices = tileRef.Value.GridIndices;
                var region = SharedMapSystem.GetChunkIndices(indices, size);

                if (!layer.ScanTiles.TryGetValue(region, out var tiles))
                {
                    tiles = new ushort[size * size];
                    layer.ScanTiles[region] = tiles;
                }

                var local = indices - region * size;
                tiles[local.X + local.Y * size] = (ushort) tileRef.Value.Tile.TypeId;
            }
        }
        catch (Exception)
        {
            // The grid gained or lost a chunk between frames, which invalidates the enumerator
            // (InvalidOperationException, or a debug assert if a chunk emptied under us). Start over.
            layer.ScanRator = null;
            layer.ScanTiles.Clear();
            layer.ScanRestarts++;
            return;
        }

        if (!finished)
        {
            // Struct enumerator: store the advanced copy back.
            layer.ScanRator = rator;
            return;
        }

        _snapshotScratch.Clear();
        foreach (var (region, tiles) in layer.ScanTiles)
        {
            _snapshotScratch.Add(new StructureSnapshot(region, region * size, size, tiles, layer.NextSequence(region)));
        }

        layer.ScanTiles.Clear();
        layer.ScanRator = null;
        layer.Scanned = true;

        // Changes seen during the scan may predate the copy of their region; recheck them.
        foreach (var region in layer.Dirty)
        {
            _snapshotScratch.Add(SnapshotRegion(cache, grid, layer, region));
        }

        layer.Dirty.Clear();
        QueueStructureJobs(cache);
    }

    private StructureSnapshot SnapshotRegion(TerrainCache cache, MapGridComponent grid, StructureLayer layer, Vector2i region)
    {
        const int size = StructureRegionSize;
        var origin = region * size;
        var tiles = new ushort[size * size];

        // Inset so neighbouring regions' tiles aren't included.
        var box = new Box2(origin + new Vector2(0.01f, 0.01f), origin + new Vector2(size - 0.01f, size - 0.01f));
        var rator = _mapSystem.GetLocalTilesEnumerator(cache.MapUid, grid, box);

        while (rator.MoveNext(out var tileRef))
        {
            var local = tileRef.GridIndices - origin;
            if ((uint) local.X >= size || (uint) local.Y >= size)
                continue;

            tiles[local.X + local.Y * size] = (ushort) tileRef.Tile.TypeId;
        }

        return new StructureSnapshot(region, origin, size, tiles, layer.NextSequence(region));
    }

    /// <summary>
    /// Batches snapshots into jobs. They wait their turn in <see cref="_structureQueue"/> started by
    /// <see cref="DispatchStructureJobs"/>, so a big first scan never floods the thread pool anymore.
    /// </summary>
    private void QueueStructureJobs(TerrainCache cache)
    {
        if (_snapshotScratch.Count == 0)
            return;

        var sampler = cache.Sampler;
        var version = cache.Version;

        for (var start = 0; start < _snapshotScratch.Count; start += StructureRegionsPerJob)
        {
            var count = Math.Min(StructureRegionsPerJob, _snapshotScratch.Count - start);
            var batch = _snapshotScratch.GetRange(start, count).ToArray();
            _structureQueue.Enqueue(new StructureJob(cache, sampler, version, batch));
        }

        _snapshotScratch.Clear();
    }

    /// <summary>
    /// Starts the queued structure jobs up to <see cref="_maxStructureWorkers"/>. Structure work has its own counter,
    /// separate from terrain chunks, so it wont take the slots terrain generation needs.
    /// </summary>
    private void DispatchStructureJobs()
    {
        while (_structureRunning < _maxStructureWorkers && _structureQueue.TryDequeue(out var job))
        {
            // Planet was rebuilt or released while this waited; its regions get rescanned anyway.
            if (job.Version != job.Cache.Version)
                continue;

            Interlocked.Increment(ref _structureRunning);
            Task.Run(() => RunStructureJob(job.Cache, job.Sampler, job.Version, job.Batch));
        }
    }

    private void RunStructureJob(TerrainCache cache, PlanetTerrainSampler sampler, int version, StructureSnapshot[] batch)
    {
        var results = new (StructureSnapshot Snapshot, bool[]? Mask)[batch.Length];

        try
        {
            if (!_shutdown)
            {
                for (var i = 0; i < batch.Length; i++)
                {
                    var snap = batch[i];
                    bool[]? mask = null;

                    for (var y = 0; y < snap.Size; y++)
                    {
                        for (var x = 0; x < snap.Size; x++)
                        {
                            var index = x + y * snap.Size;
                            var type = snap.Tiles[index];

                            if (type == 0)
                                continue;

                            if (sampler.SampleTile(new Vector2i(snap.Origin.X + x, snap.Origin.Y + y)) == type)
                                continue;

                            mask ??= new bool[snap.Size * snap.Size];
                            mask[index] = true;
                        }
                    }

                    results[i] = (snap, mask);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to scan planet structures: {e}");
        }
        finally
        {
            _structureResults.Enqueue(new StructureResult(cache, version, results));
            Interlocked.Decrement(ref _structureRunning);
        }
    }

    private void ApplyStructureResults()
    {
        while (_structureResults.TryDequeue(out var result))
        {
            var cache = result.Cache;
            if (result.Version != cache.Version)
                continue;

            var layer = cache.Structures;
            _rebuildScratch.Clear();

            foreach (var (snap, mask) in result.Results)
            {
                // Skipped by an exception, or superseded by a newer snapshot of the same region.
                if (snap.Tiles == null || !layer.IsLatest(snap.Region, snap.Sequence))
                    continue;

                if (mask != null)
                    layer.Masks[snap.Region] = mask;
                else if (!layer.Masks.Remove(snap.Region))
                    continue; // Was empty, still empty.

                // Edges at region borders depend on the neighbours.
                _rebuildScratch.Add(snap.Region);
                _rebuildScratch.Add(snap.Region + Vector2i.Up);
                _rebuildScratch.Add(snap.Region + Vector2i.Down);
                _rebuildScratch.Add(snap.Region + Vector2i.Left);
                _rebuildScratch.Add(snap.Region + Vector2i.Right);
            }

            foreach (var region in _rebuildScratch)
            {
                layer.RebuildGeometry(region);
            }
        }
    }

    public sealed class StructureLayer
    {
        internal const int Size = StructureRegionSize;

        internal readonly Dictionary<Vector2i, bool[]> Masks = new();
        internal readonly Dictionary<Vector2i, StructureGeometry> Geometry = new();
        internal readonly HashSet<Vector2i> Dirty = new();
        private readonly Dictionary<Vector2i, int> _sequence = new();

        // In-progress first scan.
        internal readonly Dictionary<Vector2i, ushort[]> ScanTiles = new();
        internal GridTileEnumerator? ScanRator;
        internal int ScanRestarts;

        internal bool Scanned;
        internal long LastViewed = -1;

        internal void Reset()
        {
            Masks.Clear();
            Geometry.Clear();
            Dirty.Clear();
            _sequence.Clear();
            ScanTiles.Clear();
            ScanRator = null;
            ScanRestarts = 0;
            Scanned = false;
        }

        internal int NextSequence(Vector2i region)
        {
            // Not GetValueOrDefault: CollectionExtensions isn't in the sandbox whitelist.
            var next = (_sequence.TryGetValue(region, out var current) ? current : 0) + 1;
            _sequence[region] = next;
            return next;
        }

        internal bool IsLatest(Vector2i region, int sequence)
        {
            return _sequence.TryGetValue(region, out var latest) && latest == sequence;
        }

        private bool IsForeign(Vector2i region, int x, int y)
        {
            if (x < 0) { region += Vector2i.Left; x += Size; }
            else if (x >= Size) { region += Vector2i.Right; x -= Size; }

            if (y < 0) { region += Vector2i.Down; y += Size; }
            else if (y >= Size) { region += Vector2i.Up; y -= Size; }

            return Masks.TryGetValue(region, out var mask) && mask[x + y * Size];
        }

        internal void RebuildGeometry(Vector2i region)
        {
            if (!Masks.TryGetValue(region, out var mask))
            {
                Geometry.Remove(region);
                return;
            }

            var origin = (Vector2) (region * Size);
            var fill = new List<Vector2>();
            var edges = new List<Vector2>();

            for (var y = 0; y < Size; y++)
            {
                // Fill: merge each row into runs, two triangles per run.
                var runStart = -1;
                for (var x = 0; x <= Size; x++)
                {
                    var foreign = x < Size && mask[x + y * Size];

                    if (foreign && runStart < 0)
                    {
                        runStart = x;
                    }
                    else if (!foreign && runStart >= 0)
                    {
                        var bl = origin + new Vector2(runStart, y);
                        var tr = origin + new Vector2(x, y + 1);
                        var br = new Vector2(tr.X, bl.Y);
                        var tl = new Vector2(bl.X, tr.Y);
                        fill.Add(bl); fill.Add(br); fill.Add(tr);
                        fill.Add(bl); fill.Add(tr); fill.Add(tl);
                        runStart = -1;
                    }
                }

                // Edges: a side is an outline wherever the neighbour isn't a structure tile.
                for (var x = 0; x < Size; x++)
                {
                    if (!mask[x + y * Size])
                        continue;

                    var bl = origin + new Vector2(x, y);
                    var tr = bl + Vector2.One;

                    if (!IsForeign(region, x, y - 1)) { edges.Add(bl); edges.Add(new Vector2(tr.X, bl.Y)); }
                    if (!IsForeign(region, x, y + 1)) { edges.Add(new Vector2(bl.X, tr.Y)); edges.Add(tr); }
                    if (!IsForeign(region, x - 1, y)) { edges.Add(bl); edges.Add(new Vector2(bl.X, tr.Y)); }
                    if (!IsForeign(region, x + 1, y)) { edges.Add(new Vector2(tr.X, bl.Y)); edges.Add(tr); }
                }
            }

            Geometry[region] = new StructureGeometry(
                new Box2(origin, origin + new Vector2(Size, Size)),
                fill.ToArray(),
                edges.ToArray());
        }
    }

    public sealed record StructureGeometry(Box2 Bounds, Vector2[] Fill, Vector2[] Edges);

    private readonly record struct StructureSnapshot(Vector2i Region, Vector2i Origin, int Size, ushort[] Tiles, int Sequence);

    private readonly record struct StructureJob(TerrainCache Cache, PlanetTerrainSampler Sampler, int Version, StructureSnapshot[] Batch);

    private readonly record struct StructureResult(TerrainCache Cache, int Version, (StructureSnapshot Snapshot, bool[]? Mask)[] Results);
}
