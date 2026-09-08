using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Layers;
using Robust.Shared.Noise;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Shared._Mono.Radar;

public sealed class RadarTerrainSampler
{
    private readonly record struct Layer(FastNoiseLite Noise, float Threshold, bool Invert,
        Color Color, RadarTerrainSampler? Children, float OriginBiasRadius, float OriginBiasStrength,
        int Tile, int[]? AllowedTiles, BiomeMetaLayer? Bias, int CacheOffset = 0);

    private readonly Layer[] _layers;
    private readonly int[] _entityTiles;
    private readonly bool _hasTiles;
    private readonly int _matchCount;
    private readonly float _wrapSize;
    public readonly Dictionary<string, Color> EntityColors = new();

    public RadarTerrainSampler(List<IBiomeLayer> layers, int seed, IPrototypeManager prototypes,
        ISerializationManager serialization, bool includeEntities = true, float wrapSize = 0f)
    {
        _wrapSize = wrapSize;
        var result = new List<Layer>();
        var entityTiles = new HashSet<int>();
        foreach (var layer in layers)
        {
            if (layer is not BiomeTileLayer && layer is not BiomeMetaLayer && layer is not BiomeEntityLayer)
                continue;
            if (!includeEntities && layer is BiomeEntityLayer)
                continue;

            var noise = new FastNoiseLite();
            serialization.CopyTo(layer.Noise, ref noise, notNullableOverride: true);
            noise.SetSeed(noise.GetSeed() + seed);
            noise.SetFractalOctaves(noise.GetFractalOctaves());
            var color = Color.Transparent;
            var tileId = 0;
            int[]? allowedTiles = null;
            RadarTerrainSampler? children = null;
            if (layer is BiomeTileLayer tile)
            {
                var tint = prototypes.Index(tile.Tile).RadarColor;
                color = tint.WithAlpha(0.55f);
                tileId = prototypes.Index(tile.Tile).TileId;
                _hasTiles = true;
            }
            else if (layer is BiomeEntityLayer entity)
            {
                var tint = entity.RadarColor;
                color = tint.WithAlpha(0.75f);
                foreach (var prototype in entity.Entities)
                    EntityColors[prototype] = color;
                allowedTiles = new int[entity.AllowedTiles.Count];
                for (var i = 0; i < allowedTiles.Length; i++)
                    allowedTiles[i] = prototypes.Index<ContentTileDefinition>(entity.AllowedTiles[i]).TileId;
                entityTiles.UnionWith(allowedTiles);
            }
            else if (layer is BiomeMetaLayer meta)
            {
                children = new RadarTerrainSampler(meta.Layers ?? prototypes.Index<BiomeTemplatePrototype>(meta.Template).Layers,
                    seed, prototypes, serialization, includeEntities, wrapSize);
                foreach (var (prototype, tint) in children.EntityColors)
                    EntityColors[prototype] = tint;
                _hasTiles |= children._hasTiles;
                entityTiles.UnionWith(children._entityTiles);
            }
            var bias = layer as BiomeMetaLayer;
            result.Add(new Layer(noise, layer.Threshold, layer.Invert, color, children,
                bias?.OriginBiasRadius ?? 0f, bias?.OriginBiasStrength ?? 0f, tileId, allowedTiles, bias));
        }
        _layers = result.ToArray();
        _matchCount = _layers.Length;
        for (var i = 0; i < _layers.Length; i++)
        {
            if (_layers[i].Children is not { } child)
                continue;
            _layers[i] = _layers[i] with { CacheOffset = _matchCount };
            _matchCount += child._matchCount;
        }
        _entityTiles = new int[entityTiles.Count];
        entityTiles.CopyTo(_entityTiles);
    }

    private bool Matches(in Layer layer, int x, int y)
    {
        if (layer.Threshold <= -1f && layer.OriginBiasStrength == 0f)
            return true;

        var bias = layer.Bias?.GetBias(x, y) ?? 0f;
        var threshold = layer.Threshold - bias;
        if (threshold < -1f)
            return true;
        if (threshold > 1f)
            return false;

        var value = SharedBiomeSystem.SampleTerrain(layer.Noise, new Vector2i(x, y), _wrapSize);
        return (layer.Invert ? -value : value) + bias >= layer.Threshold;
    }

    public byte[] CreateCache() => new byte[_matchCount];

    public uint Sample(int x, int y, byte[]? cache = null)
        => Sample(x, y, out _, out _, cache);

    public uint Sample(int x, int y, out int tile, out Color ground, byte[]? cache = null)
    {
        Span<byte> matches = cache;
        matches.Clear();
        ground = SampleTile(x, y, out tile, matches);
        var entity = SampleEntity(x, y, tile, matches);
        return Pack(entity.A != 0 ? entity : ground);
    }

    private bool Matches(in Layer layer, int x, int y, Span<byte> cache, int index)
    {
        if (cache.IsEmpty)
            return Matches(layer, x, y);
        if (cache[index] == 0)
            cache[index] = Matches(layer, x, y) ? (byte)2 : (byte)1;
        return cache[index] == 2;
    }

    public static uint Pack(Color color)
    {
        return (uint)(color.R * 255f) | (uint)(color.G * 255f) << 8 |
            (uint)(color.B * 255f) << 16 | (uint)(color.A * 255f) << 24;
    }

    public Color SampleTile(int x, int y, out int tile)
        => SampleTile(x, y, out tile, Span<byte>.Empty);

    private Color SampleTile(int x, int y, out int tile, Span<byte> matches)
    {
        tile = 0;
        for (var i = _layers.Length - 1; i >= 0; i--)
        {
            ref readonly var layer = ref _layers[i];
            if (layer.AllowedTiles != null || layer.Children is { _hasTiles: false } || !Matches(layer, x, y, matches, i))
                continue;

            tile = layer.Tile;
            var color = layer.Color;
            if (layer.Children is { } child)
            {
                var cache = matches.IsEmpty ? matches : matches.Slice(layer.CacheOffset, child._matchCount);
                color = child.SampleTile(x, y, out tile, cache);
            }
            if (color.A != 0)
                return color;
        }
        return Color.Transparent;
    }

    public Color SampleEntity(int x, int y, int tile)
        => SampleEntity(x, y, tile, Span<byte>.Empty);

    private Color SampleEntity(int x, int y, int tile, Span<byte> matches)
    {
        if (Array.IndexOf(_entityTiles, tile) < 0)
            return Color.Transparent;
        for (var i = _layers.Length - 1; i >= 0; i--)
        {
            ref readonly var layer = ref _layers[i];
            if (layer.Children == null && (layer.AllowedTiles == null || Array.IndexOf(layer.AllowedTiles, tile) < 0))
                continue;
            if (layer.Children != null && Array.IndexOf(layer.Children._entityTiles, tile) < 0)
                continue;
            if (!Matches(layer, x, y, matches, i))
                continue;

            var color = layer.Color;
            if (layer.Children is { } child)
            {
                var cache = matches.IsEmpty ? matches : matches.Slice(layer.CacheOffset, child._matchCount);
                color = child.SampleEntity(x, y, tile, cache);
            }
            if (color.A != 0)
                return color;
        }
        return Color.Transparent;
    }
}
