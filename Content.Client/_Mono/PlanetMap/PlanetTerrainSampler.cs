using Content.Shared.Parallax.Biomes;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Mono.PlanetMap;

/// <summary>
/// Turns a <see cref="CompiledBiome"/> into map colours. Hopefully Thread-safe and hopefully immutable.
/// </summary>
/// <remarks>
/// Did you know you can add remarks to summaries? All the rules live in <see cref="CompiledBiome"/>, the same code <c>SharedBiomeSystem</c>
/// uses for generation, any change to planet generation SHOULD show up here automatically.
/// Otherwise this class only adds colours and the world border.
/// </remarks>
public sealed class PlanetTerrainSampler
{
    private readonly CompiledBiome _biome;
    private readonly Rgba32[] _tileColors;

    /// <summary>
    /// Map colour per compiled layer, indexed by <see cref="CompiledBiomeLayer.Id"/>.
    /// Alpha is how much the layer's entities cover the tile. 0 means draw the tile. Probably overengineered for what we're doing.
    /// </summary>
    private readonly Rgba32[] _layerColors;

    private readonly PlanetMapBorder? _border;

    public readonly int Seed;

    /// <summary>
    /// Using this to detect when the biome data or border changed to rebuild cache.
    /// </summary>
    public readonly int Fingerprint;

    internal PlanetTerrainSampler(CompiledBiome biome, Rgba32[] tileColors, Rgba32[] layerColors, PlanetMapBorder? border, int fingerprint)
    {
        _biome = biome;
        _tileColors = tileColors;
        _layerColors = layerColors;
        _border = border;
        Seed = biome.Seed;
        Fingerprint = fingerprint;
    }

    public PlanetMapBorder? Border => _border;

    /// <summary>
    /// Fills a <paramref name="texels"/> x <paramref name="texels"/> image, row 0 is northern edge.
    /// Each texel covers <paramref name="step"/> x <paramref name="step"/> tiles and is sampled at its centre tile.
    /// Texels outside the world border are transparent. Yes, they should probably not be drawn at all, whatever, TODO.
    /// </summary>
    public void FillChunk(Span<Rgba32> output, int originX, int originY, int texels, int step)
    {
        var half = step / 2;

        for (var row = 0; row < texels; row++)
        {
            var y = originY + (texels - 1 - row) * step + half;
            var rowOffset = row * texels;

            for (var col = 0; col < texels; col++)
            {
                var x = originX + col * step + half;

                output[rowOffset + col] = _border is { } border && !border.Contains(x + 0.5f, y + 0.5f)
                    ? default
                    : Sample(new Vector2i(x, y));
            }
        }
    }

    /// <summary>
    /// The seeded tile type here, or -1 if the biome leaves it empty.
    /// </summary>
    public int SampleTile(Vector2i indices)
    {
        var real = new NoRealTiles();

        if (!CompiledBiome.TryGetTileLayer(_biome.Layers, indices, ref real, out var layer, out _) || layer?.TileDef == null)
            return -1;

        return layer.TileDef.TileId;
    }

    public Rgba32 Sample(Vector2i indices)
    {
        var tile = SampleTile(indices);
        if (tile < 0)
            return default;

        var color = (uint) tile < (uint) _tileColors.Length ? _tileColors[tile] : default;

        if (!CompiledBiome.TryGetEntityLayer(_biome.Layers, indices, tile, out var layer) || layer == null)
            return color;

        var entityColor = _layerColors[layer.Id];

        if (entityColor.A == 255)
            return entityColor;

        return entityColor.A == 0 ? color : Blend(color, entityColor);
    }

    private static Rgba32 Blend(Rgba32 under, Rgba32 over)
    {
        var a = over.A / 255f;
        return new Rgba32(
            (byte) (under.R + (over.R - under.R) * a),
            (byte) (under.G + (over.G - under.G) * a),
            (byte) (under.B + (over.B - under.B) * a),
            Math.Max(under.A, over.A));
    }
}
