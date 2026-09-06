using Content.Shared._Mono.Radar;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using System.Threading.Tasks;
using Content.Shared.Parallax.Biomes;
using Content.Shared._Mono.Planets;
using Content.Shared.Parallax.Biomes.Layers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Configuration;
using Content.Shared._Mono.CCVar;
using System.Numerics;
using System.Threading;
using Robust.Client.Player;
using Robust.Shared.Map.Components;
using Content.Shared._Mono.Detection;
using Content.Shared._CE.ZLevels.Core.EntitySystems;

namespace Content.Client._Mono.Radar;

public sealed class RadarTerrainSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private DetectionSystem _detection = default!;
    [Dependency] private CESharedZLevelsSystem _levels = default!;

    private sealed class Chart
    {
        public readonly Dictionary<RadarTerrainChunk, uint[]> Chunks = new();
        public readonly Dictionary<RadarTerrainChunk, TimeSpan> Pending = new();
        public readonly HashSet<RadarTerrainChunk> Stale = new();
        public int Revision;
        public Dictionary<RadarTerrainChunk, uint[]>? Snapshot;
        public readonly Dictionary<RadarTerrainChunk, uint[]> Preview = new();
        public readonly Queue<RadarTerrainChunk> PreviewOrder = new();
        public Task<Dictionary<RadarTerrainChunk, uint[]>>[]? PreviewJobs;
        public RadarTerrainSampler? Sampler;
        public List<IBiomeLayer>? Layers;
        public int Seed;
        public readonly Scan Visible = new();
        public readonly Scan Prefetch = new();
        public Vector2i RequestMin;
        public Vector2i RequestMax;
        public Vector2i RequestCursor;
    }

    private sealed class Scan
    {
        public Vector2i Min;
        public Vector2i Max;
        public int Ring;
        public int Offset;
        public bool Started;
        public bool Complete;
    }

    private readonly Dictionary<EntityUid, Chart> _charts = new();
    private readonly List<RadarTerrainChunk> _requests = new();
    private TimeSpan _nextRequest;
    private TimeSpan _lastRadarRequest;

    public override void Update(float frameTime)
    {
        if (_timing.RealTime - _lastRadarRequest < TimeSpan.FromSeconds(0.25) ||
            _player.LocalEntity is not { } player || !TryComp<TransformComponent>(player, out var xform))
            return;

        Entity<MapGridComponent>? surface = null;
        var altitude = _levels.GetAbsoluteAltitude(player);
        var nearest = float.NegativeInfinity;
        var query = EntityQueryEnumerator<BiomeComponent, MapGridComponent>();
        while (query.MoveNext(out var uid, out _, out var grid))
        {
            if (!_detection.SharesRadarSpace(xform.MapUid, uid))
                continue;
            var depth = _levels.GetAbsoluteAltitude(uid);
            if (depth > altitude + 0.05f || depth < nearest)
                continue;
            nearest = depth;
            surface = (uid, grid);
        }
        if (surface is not { } terrain)
            return;
        if (!_charts.TryGetValue(terrain.Owner, out var chart))
            _charts[terrain.Owner] = chart = new Chart();
        var center = _maps.WorldToLocal(terrain.Owner, terrain.Comp, _transform.GetWorldPosition(player)) / terrain.Comp.TileSize;
        UpdatePreview(terrain.Owner, chart, new Box2(center - new Vector2(64), center + new Vector2(64)), 1);
    }

    public override void Initialize()
    {
        SubscribeNetworkEvent<RadarTerrainChunkEvent>(OnChunk);
        SubscribeNetworkEvent<RadarTerrainInvalidatedEvent>(OnInvalidated);
        SubscribeLocalEvent<Content.Shared.Parallax.Biomes.BiomeComponent, ComponentShutdown>(OnShutdown);
    }

    public int Request(EntityUid console, EntityUid map, Box2 bounds, int step)
    {
        _lastRadarRequest = _timing.RealTime;
        if (!_charts.TryGetValue(map, out var chart))
            _charts[map] = chart = new Chart();
        UpdatePreview(map, chart, bounds, step);
        if (_timing.RealTime < _nextRequest)
            return chart.Revision;

        _requests.Clear();
        if (chart.Pending.Count > 4096)
            chart.Pending.Clear();
        var size = RadarTerrainChunk.Size * step;
        var left = (int)MathF.Floor(bounds.Left / size);
        var bottom = (int)MathF.Floor(bounds.Bottom / size);
        var right = (int)MathF.Floor(bounds.Right / size);
        var top = (int)MathF.Floor(bounds.Top / size);
        var min = new Vector2i(left, bottom);
        var max = new Vector2i(right, top);
        if (chart.RequestMin != min || chart.RequestMax != max)
        {
            chart.RequestMin = min;
            chart.RequestMax = max;
            chart.RequestCursor = min;
        }
        var start = chart.RequestCursor;
        for (var visited = 0; visited < 4096 && _requests.Count < 16; visited++)
        {
            var chunk = new RadarTerrainChunk(chart.RequestCursor, step);
            chart.RequestCursor = new Vector2i(chart.RequestCursor.X + 1, chart.RequestCursor.Y);
            if (chart.RequestCursor.X > right)
                chart.RequestCursor = new Vector2i(left, chart.RequestCursor.Y + 1);
            if (chart.RequestCursor.Y > top)
                chart.RequestCursor = min;
            if (visited > 0 && chunk.Index == start)
                break;
            if (!chart.Preview.ContainsKey(chunk))
                continue;
            if (chart.Chunks.ContainsKey(chunk) && !chart.Stale.Contains(chunk) ||
                chart.Pending.TryGetValue(chunk, out var sent) && _timing.RealTime - sent < TimeSpan.FromSeconds(3))
                continue;
            _requests.Add(chunk);
            chart.Pending[chunk] = _timing.RealTime;
        }
        if (_requests.Count > 0)
        {
            RaiseNetworkEvent(new RequestRadarTerrainEvent(GetNetEntity(console), GetNetEntity(map), _requests.ToArray()));
        }
        _nextRequest = _timing.RealTime + TimeSpan.FromSeconds(0.15);
        return chart.Revision;
    }

    public Dictionary<RadarTerrainChunk, uint[]> Snapshot(EntityUid map)
    {
        var chart = _charts[map];
        if (chart.Snapshot != null)
            return chart.Snapshot;
        var snapshot = new Dictionary<RadarTerrainChunk, uint[]>(chart.Preview);
        foreach (var (chunk, pixels) in chart.Chunks)
            snapshot[chunk] = pixels;
        return chart.Snapshot = snapshot;
    }

    private void UpdatePreview(EntityUid map, Chart chart, Box2 bounds, int step)
    {
        if (!TryComp<BiomeComponent>(map, out var biome))
            return;
        if (chart.Sampler == null || chart.Layers != biome.Layers || chart.Seed != biome.Seed)
        {
            chart.Sampler = new RadarTerrainSampler(biome.Layers, biome.Seed, _prototypes, _serialization,
                wrapSize: CompOrNull<ToroidalMapComponent>(map)?.Size ?? 0f);
            chart.Layers = biome.Layers;
            chart.Seed = biome.Seed;
            chart.Preview.Clear();
            chart.PreviewOrder.Clear();
            chart.Chunks.Clear();
            chart.Pending.Clear();
            chart.Stale.Clear();
            chart.PreviewJobs = null;
            chart.Snapshot = null;
            chart.Revision++;
            chart.Visible.Started = false;
            chart.Prefetch.Started = false;
        }
        var prefetchCenter = SharedMapSystem.GetChunkIndices(bounds.Center, 256) * 256;
        var prefetch = new Box2((Vector2)prefetchCenter - new Vector2(2048), (Vector2)prefetchCenter + new Vector2(2048));
        var active = false;
        var updated = false;
        if (chart.PreviewJobs is { } jobs)
        {
            for (var i = 0; i < jobs.Length; i++)
            {
                var job = jobs[i];
                if (job == null)
                    continue;
                if (!job.IsCompleted)
                {
                    active = true;
                    continue;
                }
                foreach (var (chunk, pixels) in job.GetAwaiter().GetResult())
                {
                    if (!chart.Preview.ContainsKey(chunk))
                        chart.PreviewOrder.Enqueue(chunk);
                    chart.Preview[chunk] = pixels;
                }
                jobs[i] = null!;
                updated = true;
            }
        }
        if (updated)
        {
            TrimPreview(chart, bounds, prefetch, step);
            chart.Snapshot = null;
            chart.Revision++;
        }
        if (active)
            return;
        if (ScanFinished(chart.Visible, bounds, step) && ScanFinished(chart.Prefetch, prefetch, step))
            return;

        var budget = Math.Clamp(_configuration.GetCVar(MonoCVars.RadarTerrainBatchSize), 16, 2048);
        var missing = new List<RadarTerrainChunk>(budget);
        CollectMissing(chart, chart.Visible, bounds, step, budget, missing);
        if (chart.Visible.Complete && missing.Count == 0)
            CollectMissing(chart, chart.Prefetch, prefetch, step, budget, missing);
        if (missing.Count == 0)
            return;

        var sampler = chart.Sampler;
        var workers = Math.Min(missing.Count, Math.Min(Math.Clamp(_configuration.GetCVar(MonoCVars.RadarTerrainWorkers), 1, 16),
            Environment.ProcessorCount));
        var nextChunk = -1;
        chart.PreviewJobs = new Task<Dictionary<RadarTerrainChunk, uint[]>>[workers];
        for (var worker = 0; worker < workers; worker++)
        {
            chart.PreviewJobs[worker] = Task.Run(() =>
            {
                var cache = sampler.CreateCache();
                var result = new Dictionary<RadarTerrainChunk, uint[]>((missing.Count + workers - 1) / workers);
                for (var i = Interlocked.Increment(ref nextChunk); i < missing.Count; i = Interlocked.Increment(ref nextChunk))
                {
                    var chunk = missing[i];
                    var pixels = new uint[RadarTerrainChunk.Size * RadarTerrainChunk.Size];
                    var origin = chunk.Origin;
                    for (var y = 0; y < RadarTerrainChunk.Size; y++)
                    for (var x = 0; x < RadarTerrainChunk.Size; x++)
                        pixels[y * RadarTerrainChunk.Size + x] = sampler.Sample(origin.X + x, origin.Y + y, cache);
                    result[chunk] = pixels;
                }
                return result;
            });
        }
    }

    private static bool ScanFinished(Scan scan, Box2 bounds, int step)
    {
        var size = RadarTerrainChunk.Size * step;
        return scan.Complete && scan.Started &&
            scan.Min == SharedMapSystem.GetChunkIndices(bounds.BottomLeft, size) &&
            scan.Max == SharedMapSystem.GetChunkIndices(bounds.TopRight, size);
    }

    private static void CollectMissing(Chart chart, Scan scan, Box2 bounds, int step, int budget, List<RadarTerrainChunk> missing)
    {
        var size = RadarTerrainChunk.Size * step;
        var min = new Vector2i((int)MathF.Floor(bounds.Left / size), (int)MathF.Floor(bounds.Bottom / size));
        var max = new Vector2i((int)MathF.Floor(bounds.Right / size), (int)MathF.Floor(bounds.Top / size));
        if (!scan.Started || scan.Min != min || scan.Max != max)
        {
            scan.Min = min;
            scan.Max = max;
            scan.Ring = 0;
            scan.Offset = 0;
            scan.Complete = false;
            scan.Started = true;
        }
        if (scan.Complete)
            return;
        var center = min + (max - min) / 2;
        var radius = Math.Max(max.X - center.X, max.Y - center.Y);
        for (var visited = 0; visited < 8192 && missing.Count < budget && !scan.Complete; visited++)
        {
            var ring = scan.Ring;
            var offset = scan.Offset;
            var side = ring * 2;
            var position = ring == 0 ? center : center + (offset < side
                ? new Vector2i(ring, -ring + offset)
                : offset < side * 2 ? new Vector2i(ring - (offset - side), ring)
                : offset < side * 3 ? new Vector2i(-ring, ring - (offset - side * 2))
                : new Vector2i(-ring + (offset - side * 3), -ring));
            if (++scan.Offset >= Math.Max(1, ring * 8))
            {
                scan.Offset = 0;
                scan.Ring++;
            }
            scan.Complete = scan.Ring > radius;
            if (position.X < min.X || position.Y < min.Y || position.X > max.X || position.Y > max.Y)
                continue;
            var chunk = new RadarTerrainChunk(position, step);
            if (!chart.Preview.ContainsKey(chunk) && !chart.Chunks.ContainsKey(chunk))
                missing.Add(chunk);
        }
    }

    private void OnChunk(RadarTerrainChunkEvent args, EntitySessionEventArgs session)
    {
        if (!TryGetEntity(args.Map, out var map) || map == null || !_charts.TryGetValue(map.Value, out var chart))
            return;
        if (!chart.Preview.TryGetValue(args.Chunk, out var baseline) || args.Indices.Length != args.Pixels.Length)
            return;
        var pixels = args.Indices.Length == 0 ? baseline : (uint[])baseline.Clone();
        for (var i = 0; i < args.Indices.Length; i++)
        {
            if (args.Indices[i] < pixels.Length)
                pixels[args.Indices[i]] = args.Pixels[i];
        }
        chart.Chunks[args.Chunk] = pixels;
        chart.Pending.Remove(args.Chunk);
        chart.Stale.Remove(args.Chunk);
        chart.Revision++;
        chart.Snapshot = null;
    }

    private static void TrimPreview(Chart chart, Box2 bounds, Box2 prefetch, int step)
    {
        var remaining = chart.PreviewOrder.Count;
        while (chart.Preview.Count > 32768 && remaining-- > 0 && chart.PreviewOrder.TryDequeue(out var chunk))
        {
            var origin = chunk.Origin;
            var end = origin + new Vector2i(RadarTerrainChunk.Size * chunk.Step, RadarTerrainChunk.Size * chunk.Step);
            var chunkBounds = new Box2(origin, end);
            if (chunk.Step == step && (bounds.Intersects(chunkBounds) || prefetch.Intersects(chunkBounds)))
            {
                chart.PreviewOrder.Enqueue(chunk);
                continue;
            }
            chart.Preview.Remove(chunk);
            chart.Chunks.Remove(chunk);
            chart.Stale.Remove(chunk);
            chart.Pending.Remove(chunk);
        }
    }

    private void OnInvalidated(RadarTerrainInvalidatedEvent args, EntitySessionEventArgs session)
    {
        if (!TryGetEntity(args.Map, out var map) || map == null || !_charts.TryGetValue(map.Value, out var chart))
            return;
        if (args.Chunks.Length == 0)
        {
            chart.Sampler = null;
            chart.Stale.UnionWith(chart.Chunks.Keys);
            chart.Pending.Clear();
        }
        else
        {
            foreach (var changed in args.Chunks)
            {
                var chunk = new RadarTerrainChunk(changed, 1);
                chart.Stale.Add(chunk);
                chart.Pending.Remove(chunk);
            }
        }
    }

    private void OnShutdown(Entity<Content.Shared.Parallax.Biomes.BiomeComponent> entity, ref ComponentShutdown args)
    {
        _charts.Remove(entity.Owner);
    }
}
