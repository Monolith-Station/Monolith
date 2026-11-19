using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes.Layers;
using Robust.Shared.Map;
using Robust.Shared.Noise;

namespace Content.Shared.Parallax.Biomes;

// Mono prepared form of a biomes layers, shared by server generation and client maps.
//
// SharedBiomeSystem used to deep-copy every layers FastNoiseLite through serialization, and index metalayer templates
// A CompiledBiome does that once per (layer list, seed)
// seeded noise is built up front, meta templates are expanded into children, and tile names become ids.
// the SharedBiomeSystem walks the structure for all of its lookups, so anything reading biome terrain
// goes through the same code and stays in sync hopefully.
// A compiled biome is immutable once built, and FastNoiseLite.GetNoise only reads its configuration,
// so the walks below are safe to call from any thread, again, hopefully.

public enum CompiledBiomeLayerKind : byte
{
    Tile,
    Entity,
    Decal,
    Meta,
    Dummy,
}

public sealed class CompiledBiomeLayer
{
    /// <summary>
    /// The layer this was built from.
    /// </summary>
    public readonly IBiomeLayer Source;

    public readonly CompiledBiomeLayerKind Kind;

    /// <summary>
    /// The layers noise with the biome seed applied. Null only for dummy layers, which will never pass.
    /// </summary>
    public readonly FastNoiseLite? Noise;

    public readonly float Threshold;
    public readonly bool Invert;

    /// <summary>
    /// position of this layer within its own list. Entity picks use it as the noise Z coordinate.
    /// </summary>
    public readonly int Index;

    /// <summary>
    /// Unique within the compiled biome, for callers that want per-layer data in an array.
    /// </summary>
    public readonly int Id;

    /// <summary>
    /// Tile layers only.
    /// </summary>
    public readonly ContentTileDefinition? TileDef;

    /// <summary>
    /// Entity and decal layers only: allowed tile types, indexed by tile type id.
    /// </summary>
    public readonly bool[]? AllowedTiles;

    /// <summary>
    /// Meta layers only.
    /// </summary>
    public readonly CompiledBiomeLayer[] Children;

    internal CompiledBiomeLayer(
        IBiomeLayer source,
        CompiledBiomeLayerKind kind,
        FastNoiseLite? noise,
        int index,
        int id,
        ContentTileDefinition? tileDef,
        bool[]? allowedTiles,
        CompiledBiomeLayer[] children)
    {
        Source = source;
        Kind = kind;
        Noise = noise;
        Threshold = source.Threshold;
        Invert = source.Invert;
        Index = index;
        Id = id;
        TileDef = tileDef;
        AllowedTiles = allowedTiles;
        Children = children;
    }

    /// <summary>
    /// Whether this layers noise passes its threshold, exactly as the biome system always checked it.
    /// </summary>
    public bool Passes(int x, int y)
    {
        if (Noise == null)
            return false;

        var value = Noise.GetNoise(x, y);
        value = Invert ? value * -1 : value;
        return value >= Threshold;
    }

    public bool AllowsTile(int tileType)
    {
        return AllowedTiles != null && (uint) tileType < (uint) AllowedTiles.Length && AllowedTiles[tileType];
    }
}

/// <summary>
/// Supplies real tiles that override the seeded ones. Meta layers check this,
/// matching how the biome system has always behaved. Use <see cref="NoRealTiles"/> to ignore real tiles if you really want to.
/// </summary>
public interface IBiomeRealTiles
{
    bool TryGetRealTile(Vector2i indices, out Tile tile);
}

public struct NoRealTiles : IBiomeRealTiles
{
    public bool TryGetRealTile(Vector2i indices, out Tile tile)
    {
        tile = default;
        return false;
    }
}

public sealed class CompiledBiome
{
    public readonly CompiledBiomeLayer[] Layers;
    public readonly int Seed;

    /// <summary>
    /// Every compiled layer, including meta children, indexed by <see cref="CompiledBiomeLayer.Id"/>.
    /// </summary>
    public readonly IReadOnlyList<CompiledBiomeLayer> AllLayers;

    internal CompiledBiome(CompiledBiomeLayer[] layers, int seed, IReadOnlyList<CompiledBiomeLayer> allLayers)
    {
        Layers = layers;
        Seed = seed;
        AllLayers = allLayers;
    }

    /// <summary>
    /// Finds the tile layer that applies here, ignoring the real tile above.
    /// If a meta layer passes AND a real tile exists, that real tile wins, as it always has.
    /// </summary>
    /// <returns>True if either <paramref name="layer"/> or <paramref name="realTile"/> was found.</returns>
    public static bool TryGetTileLayer<TReal>(
        CompiledBiomeLayer[] layers,
        Vector2i indices,
        ref TReal real,
        out CompiledBiomeLayer? layer,
        out Tile? realTile) where TReal : struct, IBiomeRealTiles
    {
        for (var i = layers.Length - 1; i >= 0; i--)
        {
            var candidate = layers[i];

            if (candidate.Kind != CompiledBiomeLayerKind.Tile && candidate.Kind != CompiledBiomeLayerKind.Meta)
                continue;

            if (!candidate.Passes(indices.X, indices.Y))
                continue;

            if (candidate.Kind == CompiledBiomeLayerKind.Meta)
            {
                if (real.TryGetRealTile(indices, out var found) && !found.IsEmpty)
                {
                    layer = null;
                    realTile = found;
                    return true;
                }

                if (TryGetTileLayer(candidate.Children, indices, ref real, out layer, out realTile))
                    return true;

                continue;
            }

            // Stop. Throwing. Just skip unknown tile id's
            if (candidate.TileDef == null)
                continue;

            layer = candidate;
            realTile = null;
            return true;
        }

        layer = null;
        realTile = null;
        return false;
    }

    public static bool TryGetEntityLayer(CompiledBiomeLayer[] layers, Vector2i indices, int tileType, out CompiledBiomeLayer? layer)
    {
        for (var i = layers.Length - 1; i >= 0; i--)
        {
            var candidate = layers[i];

            switch (candidate.Kind)
            {
                case CompiledBiomeLayerKind.Dummy:
                case CompiledBiomeLayerKind.Tile:
                    continue;
                case CompiledBiomeLayerKind.Entity:
                case CompiledBiomeLayerKind.Decal:
                    if (!candidate.AllowsTile(tileType))
                        continue;
                    break;
            }

            if (!candidate.Passes(indices.X, indices.Y))
                continue;

            if (candidate.Kind == CompiledBiomeLayerKind.Meta)
            {
                if (TryGetEntityLayer(candidate.Children, indices, tileType, out layer))
                    return true;

                continue;
            }

            if (candidate.Kind != CompiledBiomeLayerKind.Entity)
            {
                layer = null;
                return false;
            }

            layer = candidate;
            return true;
        }

        layer = null;
        return false;
    }
}
