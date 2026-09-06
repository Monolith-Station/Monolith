using System.Numerics;
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

namespace Content.Server._Mono.Radar;

public sealed class RadarTerrainSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private DetectionSystem _detection = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private SharedBiomeSystem _biomes = default!;

    private sealed class Chart(int seed, List<IBiomeLayer> layers, RadarTerrainSampler sampler)
    {
        public readonly int Seed = seed;
        public readonly List<IBiomeLayer> Layers = layers;
        public readonly RadarTerrainSampler Sampler = sampler;
        public readonly byte[] SampleCache = sampler.CreateCache();
        public readonly Dictionary<RadarTerrainChunk, RadarTerrainChunkEvent> Chunks = new();
        public readonly HashSet<Vector2i> Dirty = new();
    }

    private readonly Dictionary<EntityUid, Chart> _charts = new();
    private readonly Queue<(RequestRadarTerrainEvent Request, ICommonSession Session, RadarTerrainChunk Chunk)> _requests = new();

    public override void Initialize()
    {
        SubscribeNetworkEvent<RequestRadarTerrainEvent>(OnRequest);
        SubscribeLocalEvent<TileChangedEvent>(OnTilesChanged);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating);
        SubscribeLocalEvent<AnchorStateChangedEvent>(OnAnchorChanged);
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
        if (request.Chunks.Length > 16 || _requests.Count + request.Chunks.Length > 128 || !CanRead(request, args.SenderSession, out _, out _))
            return;

        foreach (var chunk in request.Chunks)
        {
            if (chunk.Step != 1 ||
                Math.Abs((long)chunk.Index.X) >= int.MaxValue / (RadarTerrainChunk.Size * chunk.Step) ||
                Math.Abs((long)chunk.Index.Y) >= int.MaxValue / (RadarTerrainChunk.Size * chunk.Step))
                continue;
            _requests.Enqueue((request, args.SenderSession, chunk));
        }
    }

    public override void Update(float frameTime)
    {
        foreach (var (map, chart) in _charts)
        {
            if (chart.Dirty.Count == 0)
                continue;
            foreach (var changed in chart.Dirty)
                chart.Chunks.Remove(new RadarTerrainChunk(changed, 1));
            var dirty = new Vector2i[chart.Dirty.Count];
            chart.Dirty.CopyTo(dirty);
            RaiseNetworkEvent(new RadarTerrainInvalidatedEvent(GetNetEntity(map), dirty));
            chart.Dirty.Clear();
        }

        for (var count = 0; count < 2 && _requests.TryDequeue(out var pending); count++)
        {
            var (request, session, chunk) = pending;
            if (!CanRead(request, session, out var console, out var map) || !TryComp<MapGridComponent>(map, out var grid))
                continue;
            var radar = Comp<RadarConsoleComponent>(console);
            var origin = chunk.Origin;
            var center = ((Vector2)origin + new Vector2(RadarTerrainChunk.Size * chunk.Step * 0.5f)) * grid.TileSize;
            var worldCenter = Vector2.Transform(center, _transform.GetWorldMatrix(map));
            var range = radar.MaxRange * 2f + RadarTerrainChunk.Size * chunk.Step * grid.TileSize;
            if (Vector2.DistanceSquared(worldCenter, _transform.GetWorldPosition(console)) > range * range)
                continue;

            var biome = Comp<BiomeComponent>(map);
            if (!_charts.TryGetValue(map, out var chart) || chart.Seed != biome.Seed || chart.Layers != biome.Layers)
            {
                chart = new Chart(biome.Seed, biome.Layers, new RadarTerrainSampler(biome.Layers, biome.Seed, _prototypes, _serialization,
                wrapSize: CompOrNull<ToroidalMapComponent>(map)?.Size ?? 0f));
                _charts[map] = chart;
            }
            if (!chart.Chunks.TryGetValue(chunk, out var update))
            {
                if (chart.Chunks.Count >= 512)
                    chart.Chunks.Clear();
                var indices = new List<ushort>();
                var pixels = new List<uint>();
                for (var y = 0; y < RadarTerrainChunk.Size; y++)
                for (var x = 0; x < RadarTerrainChunk.Size; x++)
                {
                    var index = origin + new Vector2i(x * chunk.Step, y * chunk.Step);
                    var color = RadarTerrainSampler.Pack(Sample(map, grid, biome, chart.Sampler, index));
                    if (color == chart.Sampler.Sample(index.X, index.Y, chart.SampleCache))
                        continue;
                    indices.Add((ushort)(y * RadarTerrainChunk.Size + x));
                    pixels.Add(color);
                }
                update = new RadarTerrainChunkEvent(GetNetEntity(map), chunk, indices.ToArray(), pixels.ToArray());
                chart.Chunks[chunk] = update;
            }
            RaiseNetworkEvent(update, session);
        }
    }

    private Color Sample(EntityUid map, MapGridComponent grid, BiomeComponent biome, RadarTerrainSampler sampler, Vector2i index)
    {
        var ground = sampler.SampleTile(index.X, index.Y, out var tile);
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
                return color;
            return Color.Gray.WithAlpha(0.75f);
        }
        if (_biomes.HasRecordedTile(biome, index))
            return actual.Tile.IsEmpty ? Color.Transparent : ground;
        var entity = sampler.SampleEntity(index.X, index.Y, tile);
        return entity.A != 0 ? entity : ground;
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
        if (!TryComp<TransformComponent>(args.Entity.Owner, out var xform) || xform.GridUid is not { } map ||
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
