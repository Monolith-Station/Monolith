using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Layers;

namespace Content.Shared._Mono.Radar;

public sealed class RadarTerrainSampler
{
    private readonly CompiledBiome _biome;
    private readonly Color[] _colors;
    private readonly bool _includeEntities;
    private readonly float _wrapSize;
    public readonly Dictionary<string, Color> EntityColors = new();

    public RadarTerrainSampler(CompiledBiome biome, bool includeEntities = true, float wrapSize = 0f)
    {
        _biome = biome;
        _includeEntities = includeEntities;
        _wrapSize = wrapSize;
        _colors = new Color[biome.AllLayers.Count];
        foreach (var layer in biome.AllLayers)
        {
            if (layer.TileDef is { } tile)
                _colors[layer.Id] = tile.RadarColor.WithAlpha(0.55f);
            else if (layer.Source is BiomeEntityLayer entity)
            {
                _colors[layer.Id] = entity.RadarColor.WithAlpha(0.75f);
                foreach (var prototype in entity.Entities)
                    EntityColors[prototype] = _colors[layer.Id];
            }
        }
    }

    public uint Sample(int x, int y) => Sample(x, y, out _, out _);

    public uint Sample(int x, int y, out int tile, out Color ground)
    {
        ground = SampleTile(x, y, out tile);
        var entity = SampleEntity(x, y, tile);
        return Pack(entity.A != 0 ? entity : ground);
    }

    public static uint Pack(Color color)
    {
        return (uint)(color.R * 255f) | (uint)(color.G * 255f) << 8 |
            (uint)(color.B * 255f) << 16 | (uint)(color.A * 255f) << 24;
    }

    public Color SampleTile(int x, int y, out int tile)
    {
        var real = new NoRealTiles();
        if (CompiledBiome.TryGetTileLayer(_biome.Layers, new Vector2i(x, y), ref real, out var layer, out _, _wrapSize)
            && layer?.TileDef is { } def)
        {
            tile = def.TileId;
            return _colors[layer.Id];
        }

        tile = 0;
        return Color.Transparent;
    }

    public Color SampleEntity(int x, int y, int tile)
    {
        if (!_includeEntities || tile == 0)
            return Color.Transparent;

        return CompiledBiome.TryGetEntityLayer(_biome.Layers, new Vector2i(x, y), tile, out var layer, _wrapSize) && layer != null
            ? _colors[layer.Id]
            : Color.Transparent;
    }
}
