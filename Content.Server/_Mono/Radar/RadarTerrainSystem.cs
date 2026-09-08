using System.Numerics;
using System.Linq;
using Content.Shared._Mono.Detection;
using Content.Shared._Mono.Radar;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared._Mono.Planets;
using Content.Shared.Parallax.Biomes.Layers;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Radar;

public sealed partial class RadarTerrainSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private DetectionSystem _detection = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private SharedBiomeSystem _biomes = default!;
    [Dependency] private IGameTiming _timing = default!;


    private sealed class Chart(int seed, List<IBiomeLayer> layers, RadarTerrainSampler sampler)
    {
        public readonly int Seed = seed;
        public readonly List<IBiomeLayer> Layers = layers;
        public readonly RadarTerrainSampler Sampler = sampler;
        public readonly byte[] SampleCache = sampler.CreateCache();
        public readonly Dictionary<RadarTerrainChunk, RadarTerrainChunkEvent> Chunks = new();
        public readonly HashSet<Vector2i> Dirty = new();
        public readonly Dictionary<RadarTerrainChunk, uint> Revisions = new();
        public readonly Queue<RadarTerrainChunk> CacheOrder = new();
    }

    private readonly Dictionary<EntityUid, Chart> _charts = new();
    private readonly Queue<(RequestRadarTerrainEvent Request, ICommonSession Session, RadarTerrainChunk Chunk)> _requests = new();
    private readonly Dictionary<ICommonSession, TimeSpan> _nextRequest = new();
    private TimeSpan _nextCleanup;

    private Chart GetChart(EntityUid map)
    {
        var biome = Comp<BiomeComponent>(map);
        if (_charts.TryGetValue(map, out var chart) && chart.Seed == biome.Seed && chart.Layers == biome.Layers)
            return chart;
        return _charts[map] = new Chart(biome.Seed, biome.Layers,
            new RadarTerrainSampler(biome.Layers, biome.Seed, _prototypes, _serialization,
                wrapSize: CompOrNull<ToroidalMapComponent>(map)?.Size ?? 0f));
    }

    public override void Initialize()
    {
        SubscribeNetworkEvent<RequestRadarTerrainEvent>(OnRequest);
        SubscribeLocalEvent<TileChangedEvent>(OnTilesChanged);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating);
        SubscribeLocalEvent<AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<MoveEvent>(OnMoved);
        SubscribeLocalEvent<BiomeTerrainChangedEvent>(OnTerrainChanged);
        SubscribeLocalEvent<BiomeComponent, ComponentShutdown>(OnShutdown);
    }

    private bool CanRead(RequestRadarTerrainEvent request, ICommonSession session, out EntityUid console, out EntityUid map)
    {
        console = default;
        map = default;
        if (!TryGetEntity(request.Console, out var consoleUid) || !TryGetEntity(request.Map, out var mapUid))
            return false;
        console = consoleUid.Value;
        map = mapUid.Value;
        return session.AttachedEntity is { } actor && Exists(console) && Exists(map) &&
            HasComp<RadarConsoleComponent>(console) && HasComp<BiomeComponent>(map) &&
            _detection.SharesRadarSpace(Transform(console).MapUid, map) &&
            (_ui.IsUiOpen(console, ShuttleConsoleUiKey.Key, actor) || _ui.IsUiOpen(console, RadarConsoleUiKey.Key, actor));
    }

    private void OnRequest(RequestRadarTerrainEvent request, EntitySessionEventArgs args)
    {
        if (request.Chunks.Length > 16 || _requests.Count + request.Chunks.Length > 128
            || !CanRead(request, args.SenderSession, out var console, out var map)
            || _nextRequest.TryGetValue(args.SenderSession, out var next) && _timing.RealTime < next)
            return;

        _nextRequest[args.SenderSession] = _timing.RealTime + TimeSpan.FromMilliseconds(20);
        var chart = GetChart(map);
        var chunks = new List<RadarTerrainChunk>();
        var revisions = new List<uint>();

        foreach (var chunk in request.Chunks)
        {
            if (chunk.Step != 1 ||
                Math.Abs((long)chunk.Index.X) >= int.MaxValue / (RadarTerrainChunk.Size * chunk.Step) ||
                Math.Abs((long)chunk.Index.Y) >= int.MaxValue / (RadarTerrainChunk.Size * chunk.Step))
                continue;
            if (!TryComp<MapGridComponent>(map, out var grid) || !InRange(console, map, grid, chunk))
                continue;
            if (request.Manifest)
            {
                chunks.Add(chunk);
                if (!chart.Revisions.ContainsKey(chunk) && HasOverrides(map, grid, chunk))
                    chart.Revisions[chunk] = _timing.CurTick.Value + 1;
                revisions.Add(chart.Revisions.GetValueOrDefault(chunk));
                continue;
            }
            _requests.Enqueue((request, args.SenderSession, chunk));
        }
        if (request.Manifest)
            RaiseNetworkEvent(new RadarTerrainManifestEvent(request.Map, chunks.ToArray(), revisions.ToArray()), args.SenderSession);
    }

    private bool InRange(EntityUid console, EntityUid map, MapGridComponent grid, RadarTerrainChunk chunk)
    {
        var center = ((Vector2)chunk.Origin + new Vector2(RadarTerrainChunk.Size * 0.5f)) * grid.TileSize;
        var worldCenter = Vector2.Transform(center, _transform.GetWorldMatrix(map));
        var range = Comp<RadarConsoleComponent>(console).MaxRange * 2f + RadarTerrainChunk.Size * grid.TileSize;
        return Vector2.DistanceSquared(worldCenter, _transform.GetWorldPosition(console)) <= range * range;
    }

    private bool HasOverrides(EntityUid map, MapGridComponent grid, RadarTerrainChunk chunk)
    {
        if (_biomes.HasRecordedArea(Comp<BiomeComponent>(map), chunk.Origin, RadarTerrainChunk.Size))
            return true;
        var bounds = new Box2((Vector2)chunk.Origin * grid.TileSize,
            (Vector2)(chunk.Origin + new Vector2i(RadarTerrainChunk.Size, RadarTerrainChunk.Size)) * grid.TileSize);
        var tiles = _maps.GetLocalTilesEnumerator(map, grid, bounds);
        if (tiles.MoveNext(out _))
            return true;
        return _maps.GetLocalAnchoredEntities(map, grid, bounds).Any();
    }

    public override void Update(float frameTime)
    {
        if (_timing.RealTime >= _nextCleanup)
        {
            foreach (var session in _nextRequest.Keys.ToArray())
            {
                if (session.Status != Robust.Shared.Enums.SessionStatus.InGame)
                    _nextRequest.Remove(session);
            }
            _nextCleanup = _timing.RealTime + TimeSpan.FromSeconds(5);
        }
        foreach (var (map, chart) in _charts)
        {
            if (chart.Dirty.Count == 0)
                continue;
            foreach (var changed in chart.Dirty)
            {
                var chunk = new RadarTerrainChunk(changed, 1);
                chart.Chunks.Remove(chunk);
                chart.Revisions[chunk] = _timing.CurTick.Value + 1;
            }
            chart.Dirty.Clear();
        }

        for (var count = 0; count < 2 && _requests.TryDequeue(out var pending); count++)
        {
            var (request, session, chunk) = pending;
            if (!CanRead(request, session, out var console, out var map) || !TryComp<MapGridComponent>(map, out var grid))
                continue;
            var origin = chunk.Origin;
            if (!InRange(console, map, grid, chunk))
                continue;

            var biome = Comp<BiomeComponent>(map);
            var chart = GetChart(map);
            if (!chart.Chunks.TryGetValue(chunk, out var update))
            {
                while (chart.CacheOrder.Count >= 512 && chart.CacheOrder.TryDequeue(out var oldest))
                    chart.Chunks.Remove(oldest);
                var indices = new List<ushort>();
                var pixels = new List<uint>();
                for (var y = 0; y < RadarTerrainChunk.Size; y++)
                for (var x = 0; x < RadarTerrainChunk.Size; x++)
                {
                    var index = origin + new Vector2i(x * chunk.Step, y * chunk.Step);
                    var baseline = chart.Sampler.Sample(index.X, index.Y, out var tile, out var ground, chart.SampleCache);
                    var color = Sample(map, grid, biome, chart.Sampler, index, baseline, tile, ground);
                    if (color == baseline)
                        continue;
                    indices.Add((ushort)(y * RadarTerrainChunk.Size + x));
                    pixels.Add(color);
                }
                update = new RadarTerrainChunkEvent(GetNetEntity(map), chunk, indices.ToArray(), pixels.ToArray(),
                    chart.Revisions.GetValueOrDefault(chunk));
                chart.Chunks[chunk] = update;
                chart.CacheOrder.Enqueue(chunk);
            }
            RaiseNetworkEvent(update, session);
        }
    }

    private uint Sample(EntityUid map, MapGridComponent grid, BiomeComponent biome, RadarTerrainSampler sampler,
        Vector2i index, uint baseline, int tile, Color ground)
    {
        var originalTile = tile;
        if (_maps.TryGetTileRef(map, grid, index, out var actual) && !actual.Tile.IsEmpty)
        {
            tile = actual.Tile.TypeId;
            ground = ((ContentTileDefinition)_tiles[tile]).RadarColor.WithAlpha(0.55f);
        }
        var entities = _maps.GetAnchoredEntitiesEnumerator(map, grid, index);
        while (entities.MoveNext(out var uid))
        {
            if (TerminatingOrDeleted(uid))
                continue;
            var prototype = MetaData(uid.Value).EntityPrototype;
            if (prototype != null && sampler.EntityColors.TryGetValue(prototype.ID, out var color))
                return RadarTerrainSampler.Pack(color);
            return RadarTerrainSampler.Pack(Color.Gray.WithAlpha(0.75f));
        }
        if (_biomes.HasRecordedTile(biome, index))
            return actual.Tile.IsEmpty ? 0 : RadarTerrainSampler.Pack(ground);
        if (tile == originalTile)
            return baseline;
        var entity = sampler.SampleEntity(index.X, index.Y, tile);
        return RadarTerrainSampler.Pack(entity.A != 0 ? entity : ground);
    }

    private void OnTilesChanged(ref TileChangedEvent args)
    {
        if (!_charts.TryGetValue(args.Entity, out var chart))
            return;
        foreach (var change in args.Changes)
            chart.Dirty.Add(SharedMapSystem.GetChunkIndices(change.GridIndices, RadarTerrainChunk.Size));
    }

    private void OnEntityTerminating(ref EntityTerminatingEvent args)
    {
        if (!TryComp<TransformComponent>(args.Entity.Owner, out var xform) || !xform.Anchored || xform.GridUid is not { } map ||
            !_charts.TryGetValue(map, out var chart) || !TryComp<MapGridComponent>(map, out var grid))
            return;
        var index = _maps.LocalToTile(map, grid, xform.Coordinates);
        chart.Dirty.Add(SharedMapSystem.GetChunkIndices(index, RadarTerrainChunk.Size));
    }

    private void OnAnchorChanged(ref AnchorStateChangedEvent args)
    {
        if (args.Transform.GridUid is not { } map || !_charts.TryGetValue(map, out var chart) ||
            !TryComp<MapGridComponent>(map, out var grid))
            return;
        var index = _maps.LocalToTile(map, grid, args.Transform.Coordinates);
        chart.Dirty.Add(SharedMapSystem.GetChunkIndices(index, RadarTerrainChunk.Size));
    }

    private void OnMoved(ref MoveEvent args)
    {
        if (!args.Component.Anchored || args.OldPosition == args.NewPosition)
            return;
        MarkMoved(args.OldPosition);
        MarkMoved(args.NewPosition);
    }

    private void MarkMoved(EntityCoordinates position)
    {
        var map = position.EntityId;
        if (!_charts.TryGetValue(map, out var chart) || !TryComp<MapGridComponent>(map, out var grid))
            return;
        var index = _maps.LocalToTile(map, grid, position);
        chart.Dirty.Add(SharedMapSystem.GetChunkIndices(index, RadarTerrainChunk.Size));
    }

    private void OnTerrainChanged(ref BiomeTerrainChangedEvent args)
    {
        if (_charts.Remove(args.Map))
            RaiseNetworkEvent(new RadarTerrainInvalidatedEvent(GetNetEntity(args.Map), Array.Empty<Vector2i>()));
    }

    private void OnShutdown(Entity<BiomeComponent> entity, ref ComponentShutdown args)
    {
        _charts.Remove(entity.Owner);
    }
}
