using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes.Layers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Noise;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Utility;

namespace Content.Shared.Parallax.Biomes;

public abstract partial class SharedBiomeSystem : EntitySystem
{
    [Dependency] protected IPrototypeManager ProtoManager = default!;
    [Dependency] private ISerializationManager _serManager = default!;
    [Dependency] protected ITileDefinitionManager TileDefManager = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private SharedMapSystem _map = default!;

    protected const byte ChunkSize = 8;

    // Mono compiled biome cache.
    private readonly ConcurrentDictionary<(List<IBiomeLayer> Layers, int Seed), CompiledBiome> _compiled = new();
    private const int MaxCompiledBiomes = 128;
    private const int MaxTemplateDepth = 16;

    private T Pick<T>(List<T> collection, float value)
    {
        // Listen I don't need this exact and I'm too lazy to finetune just for random ent picking.
        value %= 1f;
        value = Math.Clamp(value, 0f, 1f);

        if (collection.Count == 1)
            return collection[0];

        var randValue = value * collection.Count;

        foreach (var item in collection)
        {
            randValue -= 1f;

            if (randValue <= 0f)
            {
                return item;
            }
        }

        throw new ArgumentOutOfRangeException();
    }

    private int Pick(int count, float value)
    {
        value %= 1f;
        value = Math.Clamp(value, 0f, 1f);

        if (count == 1)
            return 0;

        value *= count;

        for (var i = 0; i < count; i++)
        {
            value -= 1f;

            if (value <= 0f)
            {
                return i;
            }
        }

        throw new ArgumentOutOfRangeException();
    }

    #region Mono compiled biomes

    /// <summary>
    /// Gets the prepared form of a layer list for a seed, building it up on firs use.
    /// Every lookup in this system will goes through it, so generation and anything else reading
    /// biome terrain agrees.
    /// </summary>
    public CompiledBiome GetCompiledBiome(List<IBiomeLayer> layers, int seed)
    {
        var key = (layers, seed);

        if (_compiled.TryGetValue(key, out var compiled))
            return compiled;

        // Clients get a fresh layer list with every biome state, so keep the cache from growing forever.
        if (_compiled.Count >= MaxCompiledBiomes)
            _compiled.Clear();

        compiled = CompileBiome(layers, seed);
        return _compiled.GetOrAdd(key, compiled);
    }

    public void InvalidateCompiledBiomes()
    {
        _compiled.Clear();
    }

    private CompiledBiome CompileBiome(List<IBiomeLayer> layers, int seed)
    {
        var all = new List<CompiledBiomeLayer>();
        var compiled = CompileLayers(layers, seed, 0, all);
        return new CompiledBiome(compiled, seed, all);
    }

    private CompiledBiomeLayer[] CompileLayers(List<IBiomeLayer> source, int seed, int depth, List<CompiledBiomeLayer> all)
    {
        var result = new CompiledBiomeLayer[source.Count];

        for (var i = 0; i < source.Count; i++)
        {
            var layer = source[i];
            var kind = CompiledBiomeLayerKind.Dummy;
            ContentTileDefinition? tileDef = null;
            bool[]? allowed = null;
            var children = Array.Empty<CompiledBiomeLayer>();

            switch (layer)
            {
                case BiomeTileLayer tileLayer:
                    kind = CompiledBiomeLayerKind.Tile;
                    if (ProtoManager.TryIndex(tileLayer.Tile, out var def))
                        tileDef = def;
                    break;
                case BiomeEntityLayer entityLayer:
                    kind = CompiledBiomeLayerKind.Entity;
                    allowed = BuildAllowedTiles(entityLayer.AllowedTiles);
                    break;
                case BiomeDecalLayer decalLayer:
                    kind = CompiledBiomeLayerKind.Decal;
                    allowed = BuildAllowedTiles(decalLayer.AllowedTiles);
                    break;
                case BiomeMetaLayer metaLayer:
                    kind = CompiledBiomeLayerKind.Meta;
                    if (depth < MaxTemplateDepth)
                    {
                        var template = ProtoManager.Index<BiomeTemplatePrototype>(metaLayer.Template);
                        children = CompileLayers(template.Layers, seed, depth + 1, all);
                    }
                    break;
            }

            var noise = kind == CompiledBiomeLayerKind.Dummy ? null : GetNoise(layer.Noise, seed);

            var compiled = new CompiledBiomeLayer(layer, kind, noise, i, all.Count, tileDef, allowed, children);
            all.Add(compiled);
            result[i] = compiled;
        }

        return result;
    }

    private bool[] BuildAllowedTiles(List<string> names)
    {
        var ids = new List<int>(names.Count);
        var max = -1;

        foreach (var name in names)
        {
            if (!TileDefManager.TryGetDefinition(name, out var def))
                continue;

            ids.Add(def.TileId);
            max = Math.Max(max, def.TileId);
        }

        var allowed = new bool[max + 1];
        foreach (var id in ids)
        {
            allowed[id] = true;
        }

        return allowed;
    }

    /// <summary>
    /// Real tiles from a grid, for all the meta layers that defer to them.
    /// </summary>
    private struct GridRealTiles : IBiomeRealTiles
    {
        public SharedMapSystem Map;
        public Entity<MapGridComponent>? Grid;

        public bool TryGetRealTile(Vector2i indices, out Tile tile)
        {
            if (Grid is { } grid && Map.TryGetTileRef(grid, grid.Comp, indices, out var tileRef))
            {
                tile = tileRef.Tile;
                return true;
            }

            tile = default;
            return false;
        }
    }

    private bool TryGetCompiledTile(CompiledBiomeLayer[] layers, Vector2i indices, Entity<MapGridComponent>? grid, [NotNullWhen(true)] out Tile? tile)
    {
        var real = new GridRealTiles { Map = _map, Grid = grid };

        if (!CompiledBiome.TryGetTileLayer(layers, indices, ref real, out var layer, out var realTile))
        {
            tile = null;
            return false;
        }

        if (realTile != null)
        {
            tile = realTile;
            return true;
        }

        var tileLayer = (BiomeTileLayer) layer!.Source;
        return TryGetTile(indices, layer.Noise!, tileLayer.Invert, tileLayer.Threshold, layer.TileDef!, tileLayer.Variants, out tile);
    }

    private bool TryGetCompiledBiomeTile(CompiledBiomeLayer[] layers, Vector2i indices, Entity<MapGridComponent>? grid, [NotNullWhen(true)] out Tile? tile)
    {
        if (grid is { } gridEnt && _map.TryGetTileRef(gridEnt, gridEnt.Comp, indices, out var tileRef) && !tileRef.Tile.IsEmpty)
        {
            tile = tileRef.Tile;
            return true;
        }

        return TryGetCompiledTile(layers, indices, grid, out tile);
    }

    #endregion

    public bool TryGetBiomeTile(EntityUid uid, MapGridComponent grid, Vector2i indices, [NotNullWhen(true)] out Tile? tile)
    {
        if (_map.TryGetTileRef(uid, grid, indices, out var tileRef) && !tileRef.Tile.IsEmpty)
        {
            tile = tileRef.Tile;
            return true;
        }

        if (!TryComp<BiomeComponent>(uid, out var biome))
        {
            tile = null;
            return false;
        }

        return TryGetBiomeTile(indices, biome.Layers, biome.Seed, (uid, grid), out tile);
    }

    /// <summary>
    /// Tries to get the tile, real or otherwise, for the specified indices.
    /// </summary>
    public bool TryGetBiomeTile(Vector2i indices, List<IBiomeLayer> layers, int seed, Entity<MapGridComponent>? grid, [NotNullWhen(true)] out Tile? tile)
    {
        return TryGetCompiledBiomeTile(GetCompiledBiome(layers, seed).Layers, indices, grid, out tile);
    }

    /// <summary>
    /// Tries to get the tile, real or otherwise, for the specified indices.
    /// </summary>
    [Obsolete("Use the Entity<MapGridComponent>? overload")]
    public bool TryGetBiomeTile(Vector2i indices, List<IBiomeLayer> layers, int seed, MapGridComponent? grid, [NotNullWhen(true)] out Tile? tile)
    {
        return TryGetBiomeTile(indices, layers, seed, grid == null ? null : (grid.Owner, grid), out tile);
    }

    /// <summary>
    /// Gets the underlying biome tile, ignoring any existing tile that may be there.
    /// </summary>
    public bool TryGetTile(Vector2i indices, List<IBiomeLayer> layers, int seed, Entity<MapGridComponent>? grid, [NotNullWhen(true)] out Tile? tile)
    {
        return TryGetCompiledTile(GetCompiledBiome(layers, seed).Layers, indices, grid, out tile);
    }

    /// <summary>
    /// Gets the underlying biome tile, ignoring any existing tile that may be there.
    /// </summary>
    [Obsolete("Use the Entity<MapGridComponent>? overload")]
    public bool TryGetTile(Vector2i indices, List<IBiomeLayer> layers, int seed, MapGridComponent? grid, [NotNullWhen(true)] out Tile? tile)
    {
        return TryGetTile(indices, layers, seed, grid == null ? null : (grid.Owner, grid), out tile);
    }

    /// <summary>
    /// Gets the underlying biome tile, ignoring any existing tile that may be there.
    /// </summary>
    private bool TryGetTile(Vector2i indices, FastNoiseLite noise, bool invert, float threshold, ContentTileDefinition tileDef, List<byte>? variants, [NotNullWhen(true)] out Tile? tile)
    {
        var found = noise.GetNoise(indices.X, indices.Y);
        found = invert ? found * -1 : found;

        if (found < threshold)
        {
            tile = null;
            return false;
        }

        byte variant = 0;
        var variantCount = variants?.Count ?? tileDef.Variants;

        // Pick a variant tile if they're available as well
        if (variantCount > 1)
        {
            var variantValue = (noise.GetNoise(indices.X * 8, indices.Y * 8, variantCount) + 1f) * 100;
            variant = _tile.PickVariant(tileDef, (int)variantValue);
        }

        tile = new Tile(tileDef.TileId, variant);
        return true;
    }

    /// <summary>
    /// Tries to get the relevant entity for this tile.
    /// </summary>
    public bool TryGetEntity(Vector2i indices, BiomeComponent component, Entity<MapGridComponent>? grid,
        [NotNullWhen(true)] out string? entity)
    {
        if (!TryGetBiomeTile(indices, component.Layers, component.Seed, grid, out var tile))
        {
            entity = null;
            return false;
        }

        return TryGetEntity(indices, component.Layers, tile.Value, component.Seed, grid, out entity);
    }

    /// <summary>
    /// Tries to get the relevant entity for this tile.
    /// </summary>
    [Obsolete("Use the Entity<MapGridComponent>? overload")]
    public bool TryGetEntity(Vector2i indices, BiomeComponent component, MapGridComponent grid,
        [NotNullWhen(true)] out string? entity)
    {
        return TryGetEntity(indices, component, grid == null ? null : (grid.Owner, grid), out entity);
    }

    public bool TryGetEntity(Vector2i indices, List<IBiomeLayer> layers, Tile tileRef, int seed, Entity<MapGridComponent>? grid,
        [NotNullWhen(true)] out string? entity)
    {
        var compiled = GetCompiledBiome(layers, seed);

        if (!CompiledBiome.TryGetEntityLayer(compiled.Layers, indices, tileRef.TypeId, out var layer))
        {
            entity = null;
            return false;
        }

        var biomeLayer = (BiomeEntityLayer) layer!.Source;
        var noiseValue = layer.Noise!.GetNoise(indices.X, indices.Y, layer.Index);
        entity = Pick(biomeLayer.Entities, (noiseValue + 1f) / 2f);
        return true;
    }

    [Obsolete("Use the Entity<MapGridComponent>? overload")]
    public bool TryGetEntity(Vector2i indices, List<IBiomeLayer> layers, Tile tileRef, int seed, MapGridComponent grid,
        [NotNullWhen(true)] out string? entity)
    {
        return TryGetEntity(indices, layers, tileRef, seed, grid == null ? null : (grid.Owner, grid), out entity);
    }

    /// <summary>
    /// Tries to get the relevant decals for this tile.
    /// </summary>
    public bool TryGetDecals(Vector2i indices, List<IBiomeLayer> layers, int seed, Entity<MapGridComponent>? grid,
        [NotNullWhen(true)] out List<(string ID, Vector2 Position)>? decals)
    {
        return TryGetCompiledDecals(indices, GetCompiledBiome(layers, seed).Layers, grid, out decals);
    }

    private bool TryGetCompiledDecals(Vector2i indices, CompiledBiomeLayer[] layers, Entity<MapGridComponent>? grid,
        [NotNullWhen(true)] out List<(string ID, Vector2 Position)>? decals)
    {
        // Each level works out the tile from its own layers, as it always has.
        if (!TryGetCompiledBiomeTile(layers, indices, grid, out var tileRef))
        {
            decals = null;
            return false;
        }

        var tileType = tileRef.Value.TypeId;

        for (var i = layers.Length - 1; i >= 0; i--)
        {
            var layer = layers[i];

            // Entities might block decal so need to check if there's one in front of us.
            switch (layer.Kind)
            {
                case CompiledBiomeLayerKind.Dummy:
                case CompiledBiomeLayerKind.Tile:
                    continue;
                case CompiledBiomeLayerKind.Entity:
                case CompiledBiomeLayerKind.Decal:
                    if (!layer.AllowsTile(tileType))
                        continue;
                    break;
            }

            if (!layer.Passes(indices.X, indices.Y))
                continue;

            if (layer.Kind == CompiledBiomeLayerKind.Meta)
            {
                if (TryGetCompiledDecals(indices, layer.Children, grid, out decals))
                {
                    return true;
                }

                continue;
            }

            // Check if the other layer should even render, if not then keep going.
            if (layer.Kind != CompiledBiomeLayerKind.Decal)
            {
                decals = null;
                return false;
            }

            var decalLayer = (BiomeDecalLayer) layer.Source;
            var noise = layer.Noise!;
            var invert = decalLayer.Invert;
            decals = new List<(string ID, Vector2 Position)>();

            for (var x = 0; x < decalLayer.Divisions; x++)
            {
                for (var y = 0; y < decalLayer.Divisions; y++)
                {
                    var index = new Vector2(indices.X + x * 1f / decalLayer.Divisions, indices.Y + y * 1f / decalLayer.Divisions);
                    var decalValue = noise.GetNoise(index.X, index.Y);
                    decalValue = invert ? decalValue * -1 : decalValue;

                    if (decalValue < decalLayer.Threshold)
                        continue;

                    decals.Add((Pick(decalLayer.Decals, (noise.GetNoise(indices.X, indices.Y, x + y * decalLayer.Divisions) + 1f) / 2f), index));
                }
            }

            // Check other layers
            if (decals.Count == 0)
                continue;

            return true;
        }

        decals = null;
        return false;
    }

    /// <summary>
    /// Tries to get the relevant decals for this tile.
    /// </summary>
    [Obsolete("Use the Entity<MapGridComponent>? overload")]
    public bool TryGetDecals(Vector2i indices, List<IBiomeLayer> layers, int seed, MapGridComponent grid,
        [NotNullWhen(true)] out List<(string ID, Vector2 Position)>? decals)
    {
        return TryGetDecals(indices, layers, seed, grid == null ? null : (grid.Owner, grid), out decals);
    }

    private FastNoiseLite GetNoise(FastNoiseLite seedNoise, int seed)
    {
        var noiseCopy = new FastNoiseLite();
        _serManager.CopyTo(seedNoise, ref noiseCopy, notNullableOverride: true);
        noiseCopy.SetSeed(noiseCopy.GetSeed() + seed);
        // Ensure re-calculate is run.
        noiseCopy.SetFractalOctaves(noiseCopy.GetFractalOctaves());
        return noiseCopy;
    }
}
