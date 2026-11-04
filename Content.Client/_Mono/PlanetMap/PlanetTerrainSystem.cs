using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Client.Markers;
using Content.Client.Parallax;
using Content.Shared._Mono.PlanetMap;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Layers;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.ContentPack;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Mono.PlanetMap;

/// <summary>
/// World border of the planet map in world coordinates.
/// </summary>
public readonly record struct PlanetMapBorder(Vector2 Center, float Radius)
{
    public bool Contains(float x, float y)
    {
        var dx = x - Center.X;
        var dy = y - Center.Y;
        return dx * dx + dy * dy <= Radius * Radius;
    }

    public bool Intersects(Box2 box)
    {
        var closest = Vector2.Clamp(Center, box.BottomLeft, box.TopRight);
        return (closest - Center).LengthSquared() <= Radius * Radius;
    }
}

/// <summary>
/// We generate caches and draw low resolution planet terrain on the client for radar and the shuttle console map.
/// </summary>
/// <remarks>
/// Terrain comes from <see cref="SharedBiomeSystem.GetCompiledBiome"/>, the same evaluator the server generates
/// planets with, so the map should always match generation. Nothing is sent over the network for it.
/// All the terrain is split into square chunks of <see cref="ChunkTexels"/> x <see cref="ChunkTexels"/> texels.
/// Thats Texture + Pixel, in case you didn't know.
/// At level of detail <c>L</c> one texel covers <c>2^L</c> x <c>2^L</c> tiles, so every chunk costs the same
/// 1024 samples however far the view is zoomed out. Needed because our mass scanners are buffed to hell. 
/// coarse ones are tiny and generated first, so the map fills in almost immediately and then sharpens.
/// Chunks are generated on worker threads and uploaded to the GPU on the main thread with a per frame cap,
/// Results are kept in an LRU cache shared by every view.
/// </remarks>
public sealed partial class PlanetTerrainSystem : EntitySystem
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceManager _res = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public const int ChunkTexels = 32;
    public const int MaxLod = 10;

    /// <summary>
    /// Coarser LODs requested beneath the current one showing while the current LOD generates.
    /// </summary>
    private const int FallbackLods = 2;

    private const int MaxResidentChunks = 1536;
    private const int MaxUploadsPerFrame = 24;
    private static readonly ResPath ErrorRsi = new("/Textures/error.rsi");

    private readonly int _maxWorkers = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

    private readonly Dictionary<EntityUid, TerrainCache> _caches = new();
    private readonly ConcurrentQueue<ChunkResult> _completed = new();
    private readonly ConcurrentBag<Rgba32[]> _bufferPool = new();
    private readonly List<Request> _requests = new();
    private readonly Stack<OwnedTexture> _texturePool = new();
    private readonly List<(TerrainCache Cache, ChunkKey Key, ChunkEntry Entry)> _evictScratch = new();
    private readonly List<EntityUid> _deadCaches = new();
    private readonly List<ChunkKey> _pruneScratch = new();
    private readonly HashSet<EntityUid> _pendingCompile = new();
    private readonly Dictionary<EntityUid, int> _failedCompile = new();

    private readonly Dictionary<int, Rgba32> _tileColors = new();
    private readonly Dictionary<string, Rgba32> _entityColors = new();

    private int _running;
    private long _frame;
    private int _resident;
    private bool _shutdown;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        InitializeStructures();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _shutdown = true;
        ClearAll();

        while (_texturePool.TryPop(out var tex))
            tex.Dispose();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<BiomeTemplatePrototype>()
            && !args.WasModified<EntityPrototype>()
            && !args.WasModified<ContentTileDefinition>())
        {
            return;
        }

        _biome.InvalidateCompiledBiomes();
        _tileColors.Clear();
        _entityColors.Clear();
        ClearAll();
    }

    private void ClearAll()
    {
        foreach (var cache in _caches.Values)
        {
            ReleaseCache(cache);
        }

        _caches.Clear();
        _requests.Clear();
        _structureQueue.Clear();
        _pendingCompile.Clear();
        _failedCompile.Clear();
    }

    private void ReleaseCache(TerrainCache cache)
    {
        // In-flight jobs see the version change and discard their result.
        cache.Version++;

        foreach (var entry in cache.Chunks.Values)
        {
            if (entry.Texture == null)
                continue;

            RecycleTexture(entry.Texture);
            entry.Texture = null;
            _resident--;
        }

        cache.Chunks.Clear();

        // Structure masks depend on the sampler too.
        cache.Structures.Reset();
    }

    private void RecycleTexture(OwnedTexture texture)
    {
        if (_texturePool.Count < 128)
            _texturePool.Push(texture);
        else
            texture.Dispose();
    }

    /// <summary>
    /// Gets the terrain cache for a planet map.
    /// </summary>
    public TerrainStatus TryGetCache(EntityUid mapUid, out TerrainCache? cache)
    {
        cache = null;

        if (!TryComp<BiomeComponent>(mapUid, out var biome) || biome.Layers.Count == 0)
            return TerrainStatus.None;

        var border = GetBorder(mapUid);

        if (_caches.TryGetValue(mapUid, out cache) && IsCurrent(cache, biome, border))
            return TerrainStatus.Ready;

        // Don't retry a broken biome every frame; only once its data changes.
        if (_failedCompile.TryGetValue(mapUid, out var failed) && failed == GetFingerprint(biome, border))
            return TerrainStatus.None;

        _pendingCompile.Add(mapUid);
        cache = null;
        return TerrainStatus.Pending;
    }

    /// <summary>
    /// The map's world border, IF IT HAS ONE.
    /// </summary>
    public PlanetMapBorder? GetBorder(EntityUid mapUid)
    {
        if (!TryComp<PlanetMapBorderComponent>(mapUid, out var comp) || comp.Radius <= 0f)
            return null;

        return new PlanetMapBorder(comp.Offset, comp.Radius);
    }

    /// <summary>
    /// Returns the chunk texture IF it is ready, otherwise we queue it for generation and return null.
    /// Lower <paramref name="priority"/> is generated first. Call every frame for every chunk you want kept.
    /// </summary>
    public Texture? GetChunk(TerrainCache cache, ChunkKey key, float priority)
    {
        if (!cache.Chunks.TryGetValue(key, out var entry))
        {
            entry = new ChunkEntry();
            cache.Chunks[key] = entry;
        }

        entry.LastUsed = _frame;

        if (entry.State == ChunkState.Idle && entry.RequestedFrame != _frame)
        {
            entry.RequestedFrame = _frame;
            _requests.Add(new Request(cache, key, entry, priority));
        }

        return entry.Texture;
    }

    /// <summary>
    /// Whether any terrain work is still queued or running. For threading.
    /// </summary>
    public bool HasPendingWork => _running > 0 || _requests.Count > 0 || _pendingCompile.Count > 0
                                  || _structureRunning > 0 || _structureQueue.Count > 0;

    /// <summary>
    /// The bounding box of a pixel area, for all world view transform.
    /// </summary>
    public static Box2 GetViewBox(Matrix3x2 worldToView, Vector2 pixelSize)
    {
        if (!Matrix3x2.Invert(worldToView, out var viewToWorld))
            return default;

        var a = Vector2.Transform(Vector2.Zero, viewToWorld);
        var b = Vector2.Transform(new Vector2(pixelSize.X, 0f), viewToWorld);
        var c = Vector2.Transform(pixelSize, viewToWorld);
        var d = Vector2.Transform(new Vector2(0f, pixelSize.Y), viewToWorld);

        return new Box2(
            Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d)),
            Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d)));
    }

    /// <summary>
    /// Everything drawn beneath grids
    /// </summary>
    /// <param name="worldToView">World to control pixels that can rotate.</param>
    /// <param name="viewBox">World area visible, see <see cref="GetViewBox"/>.</param>
    /// <param name="center">world position at the view centre, nearer chunks are generated first.</param>
    /// <param name="pixelsPerMetre">Current zoom.</param>
    /// <param name="backingColor">The controls background, used to blend fake structure fills like grid fills.</param>
    /// <returns>Whether the map has terrain at all.</returns>
    public bool DrawUnderlay(
        DrawingHandleScreen handle,
        EntityUid mapUid,
        Matrix3x2 worldToView,
        Box2 viewBox,
        Vector2 center,
        float pixelsPerMetre,
        in PlanetDrawOptions options,
        Color backingColor)
    {
        var status = TryGetCache(mapUid, out var cache);

        if (status != TerrainStatus.Ready || cache == null)
            return status != TerrainStatus.None;

        DrawTerrain(handle, cache, viewBox, center, worldToView, pixelsPerMetre, options.PixelsPerTexel, options.TerrainModulate);

        if (options.DrawStructures)
            DrawStructures(handle, cache, viewBox, worldToView, options.StructureColor, backingColor);

        if (cache.Sampler.Border is { } border)
            DrawBorder(handle, border, worldToView, pixelsPerMetre, options.BorderColor);

        return true;
    }

    /// <summary>
    /// Draws the planes terrain, queueing any chunks that arent generated yet.
    /// </summary>
    public void DrawTerrain(
        DrawingHandleScreen handle,
        TerrainCache cache,
        Box2 viewBox,
        Vector2 center,
        Matrix3x2 worldToView,
        float pixelsPerMetre,
        float targetPixelsPerTexel = 3f,
        Color? modulate = null)
    {
        var tilesPerTexel = targetPixelsPerTexel / MathF.Max(pixelsPerMetre, 0.0001f);
        var lod = tilesPerTexel <= 1f ? 0 : (int) MathF.Ceiling(MathF.Log2(tilesPerTexel));

        if (lod > MaxLod + FallbackLods)
            return;

        lod = Math.Clamp(lod, 0, MaxLod);
        var coarsest = Math.Min(lod + FallbackLods, MaxLod);
        var border = cache.Sampler.Border;

        var quad = _quad;

        for (var level = coarsest; level >= lod; level--)
        {
            var span = ChunkTexels << level;
            var minX = (int) MathF.Floor(viewBox.Left / span);
            var maxX = (int) MathF.Floor(viewBox.Right / span);
            var minY = (int) MathF.Floor(viewBox.Bottom / span);
            var maxY = (int) MathF.Floor(viewBox.Top / span);

            var levelPriority = (coarsest - level) * 1e9f;

            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    var bl = new Vector2(x * span, y * span);
                    var tr = bl + new Vector2(span, span);

                    if (border is { } b && !b.Intersects(new Box2(bl, tr)))
                        continue;

                    var chunkCenter = (bl + tr) / 2f;
                    var priority = levelPriority + (chunkCenter - center).LengthSquared();
                    var tex = GetChunk(cache, new ChunkKey(x, y, level), priority);

                    if (tex == null)
                        continue;

                    // Transform all four corners so rotated ships work too.
                    // UV v=1 is the top of the image, which is the chunks true north.
                    var vbl = new DrawVertexUV2D(Vector2.Transform(bl, worldToView), new Vector2(0f, 0f));
                    var vbr = new DrawVertexUV2D(Vector2.Transform(new Vector2(tr.X, bl.Y), worldToView), new Vector2(1f, 0f));
                    var vtr = new DrawVertexUV2D(Vector2.Transform(tr, worldToView), new Vector2(1f, 1f));
                    var vtl = new DrawVertexUV2D(Vector2.Transform(new Vector2(bl.X, tr.Y), worldToView), new Vector2(0f, 1f));

                    quad[0] = vbl; quad[1] = vbr; quad[2] = vtr;
                    quad[3] = vbl; quad[4] = vtr; quad[5] = vtl;
                    handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, tex, quad, modulate);
                }
            }
        }
    }

    private readonly DrawVertexUV2D[] _quad = new DrawVertexUV2D[6];

    private const int BorderSegments = 96;
    private readonly Vector2[] _borderVerts = new Vector2[BorderSegments + 1];

    /// <summary>
    /// Outlines the world border.
    /// </summary>
    public void DrawBorder(DrawingHandleScreen handle, PlanetMapBorder border, Matrix3x2 worldToView, float pixelsPerMetre, Color color)
    {
        for (var i = 0; i <= BorderSegments; i++)
        {
            var angle = MathF.Tau * i / BorderSegments;
            var world = border.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * border.Radius;
            _borderVerts[i] = Vector2.Transform(world, worldToView);
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, _borderVerts, color);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _frame++;

        CompilePending();
        UploadCompleted();
        ApplyStructureResults();
        DispatchStructureWork();
        DispatchStructureJobs();
        DispatchRequests();
        EvictIfNeeded();

        // Occasionally drop caches for maps that no longer exist, and for chunks nobody looks at.
        if (_frame % 600 == 0)
        {
            _deadCaches.Clear();

            foreach (var (uid, cache) in _caches)
            {
                if (Deleted(uid) || !HasComp<BiomeComponent>(uid))
                {
                    _deadCaches.Add(uid);
                    continue;
                }

                PruneEmptyEntries(cache);
            }

            foreach (var uid in _deadCaches)
            {
                ReleaseCache(_caches[uid]);
                _caches.Remove(uid);
            }
        }
    }

    private void PruneEmptyEntries(TerrainCache cache)
    {
        _pruneScratch.Clear();

        foreach (var (key, entry) in cache.Chunks)
        {
            if (entry.Texture == null && entry.State != ChunkState.Generating && entry.LastUsed < _frame - 60)
                _pruneScratch.Add(key);
        }

        foreach (var key in _pruneScratch)
        {
            cache.Chunks.Remove(key);
        }
    }

    private void UploadCompleted()
    {
        var uploads = 0;

        while (uploads < MaxUploadsPerFrame && _completed.TryDequeue(out var result))
        {
            var entry = result.Entry;

            if (result.Version != result.Cache.Version || !_caches.ContainsValue(result.Cache))
            {
                _bufferPool.Add(result.Pixels);
                continue;
            }

            if (result.Failed)
            {
                entry.State = ChunkState.Failed;
                _bufferPool.Add(result.Pixels);
                continue;
            }

            var tex = _texturePool.TryPop(out var pooled)
                ? pooled
                : _clyde.CreateBlankTexture<Rgba32>(
                    new Vector2i(ChunkTexels, ChunkTexels),
                    "planet-terrain-chunk",
                    new TextureLoadParameters
                    {
                        SampleParameters = new TextureSampleParameters { Filter = false, WrapMode = TextureWrapMode.None },
                        Srgb = true,
                    });

            tex.SetSubImage(Vector2i.Zero, new Vector2i(ChunkTexels, ChunkTexels),
                new ReadOnlySpan<Rgba32>(result.Pixels, 0, ChunkTexels * ChunkTexels));

            _bufferPool.Add(result.Pixels);

            entry.Texture = tex;
            entry.State = ChunkState.Ready;
            _resident++;
            uploads++;
        }
    }

    private void DispatchRequests()
    {
        if (_requests.Count == 0)
            return;

        if (_running < _maxWorkers)
        {
            _requests.Sort(static (a, b) => a.Priority.CompareTo(b.Priority));

            foreach (var request in _requests)
            {
                if (_running >= _maxWorkers)
                    break;

                if (request.Entry.State != ChunkState.Idle || request.Cache.Version != request.Version)
                    continue;

                request.Entry.State = ChunkState.Generating;
                Interlocked.Increment(ref _running);

                var job = new Job(request.Cache, request.Cache.Sampler, request.Key, request.Entry, request.Version);
                Task.Run(() => RunJob(job));
            }
        }
        _requests.Clear();
    }

    private void RunJob(Job job)
    {
        var pixels = _bufferPool.TryTake(out var buf) ? buf : new Rgba32[ChunkTexels * ChunkTexels];
        var failed = false;

        try
        {
            if (!_shutdown)
            {
                var step = 1 << job.Key.Lod;
                var span = ChunkTexels * step;
                job.Sampler.FillChunk(pixels, job.Key.X * span, job.Key.Y * span, ChunkTexels, step);
            }
        }
        catch (Exception e)
        {
            failed = true;
            Log.Error($"Failed to generate planet terrain chunk {job.Key}: {e}");
        }
        finally
        {
            _completed.Enqueue(new ChunkResult(job.Cache, job.Entry, job.Version, pixels, failed));
            Interlocked.Decrement(ref _running);
        }
    }

    private void EvictIfNeeded()
    {
        if (_resident <= MaxResidentChunks)
            return;

        _evictScratch.Clear();

        foreach (var cache in _caches.Values)
        {
            foreach (var (key, entry) in cache.Chunks)
            {
                // Never evict something drawn this frame or the last one.
                if (entry.Texture != null && entry.LastUsed < _frame - 1)
                    _evictScratch.Add((cache, key, entry));
            }
        }

        _evictScratch.Sort(static (a, b) => a.Entry.LastUsed.CompareTo(b.Entry.LastUsed));

        var target = MaxResidentChunks * 3 / 4;

        foreach (var (cache, key, entry) in _evictScratch)
        {
            if (_resident <= target)
                break;

            RecycleTexture(entry.Texture!);
            entry.Texture = null;
            cache.Chunks.Remove(key);
            _resident--;
        }

        _evictScratch.Clear();
    }

    private bool IsCurrent(TerrainCache cache, BiomeComponent biome, PlanetMapBorder? border)
    {
        if (cache.Sampler.Border != border)
            return false;

        // Cheap reference check first, we only hash when the component state was actually replaced.
        if (ReferenceEquals(cache.LayersRef, biome.Layers) && cache.Sampler.Seed == biome.Seed)
            return true;

        if (GetFingerprint(biome, border) != cache.Sampler.Fingerprint)
            return false;

        cache.LayersRef = biome.Layers;
        return true;
    }

    private void CompilePending()
    {
        if (_pendingCompile.Count == 0)
            return;

        foreach (var mapUid in _pendingCompile)
        {
            if (!TryComp<BiomeComponent>(mapUid, out var biome) || biome.Layers.Count == 0)
                continue;

            var border = GetBorder(mapUid);
            var fingerprint = GetFingerprint(biome, border);

            if (_caches.TryGetValue(mapUid, out var existing) && existing.Sampler.Fingerprint == fingerprint)
            {
                existing.LayersRef = biome.Layers;
                continue;
            }

            PlanetTerrainSampler sampler;
            try
            {
                sampler = BuildSampler(biome, border, fingerprint);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to build planet terrain sampler for {ToPrettyString(mapUid)}: {e}");
                _failedCompile[mapUid] = fingerprint;
                continue;
            }

            _failedCompile.Remove(mapUid);

            if (_caches.TryGetValue(mapUid, out var cache))
            {
                ReleaseCache(cache);
                cache.Sampler = sampler;
                cache.LayersRef = biome.Layers;
            }
            else
            {
                _caches[mapUid] = new TerrainCache(mapUid, sampler, biome.Layers);
            }
        }

        _pendingCompile.Clear();
    }

    private PlanetTerrainSampler BuildSampler(BiomeComponent biome, PlanetMapBorder? border, int fingerprint)
    {
        var compiled = _biome.GetCompiledBiome(biome.Layers, biome.Seed);

        var maxTile = 0;
        foreach (var layer in compiled.AllLayers)
        {
            if (layer.TileDef != null)
                maxTile = Math.Max(maxTile, layer.TileDef.TileId);
        }

        var tileColors = new Rgba32[maxTile + 1];
        var layerColors = new Rgba32[compiled.AllLayers.Count];

        foreach (var layer in compiled.AllLayers)
        {
            if (layer.TileDef != null)
                tileColors[layer.TileDef.TileId] = GetTileColor(layer.TileDef.TileId);

            if (layer.Source is BiomeEntityLayer entityLayer)
                layerColors[layer.Id] = GetLayerEntityColor(entityLayer.Entities);
        }

        return new PlanetTerrainSampler(compiled, tileColors, layerColors, border, fingerprint);
    }

    private static int GetFingerprint(BiomeComponent biome, PlanetMapBorder? border)
    {
        var hash = new HashCode();
        hash.Add(biome.Seed);
        hash.Add(border);
        AddLayers(ref hash, biome.Layers);
        return hash.ToHashCode();
    }

    private static void AddLayers(ref HashCode hash, List<IBiomeLayer> layers)
    {
        hash.Add(layers.Count);

        foreach (var layer in layers)
        {
            switch (layer)
            {
                case BiomeDummyLayer dummy:
                    hash.Add(0);
                    hash.Add(dummy.ID);
                    continue;
                case BiomeTileLayer tile:
                    hash.Add(1);
                    hash.Add(tile.Tile.Id);
                    break;
                case BiomeEntityLayer entity:
                    hash.Add(2);
                    hash.Add(entity.Entities.Count);
                    foreach (var ent in entity.Entities)
                    {
                        hash.Add(ent);
                    }
                    hash.Add(entity.AllowedTiles.Count);
                    break;
                case BiomeDecalLayer decal:
                    hash.Add(3);
                    hash.Add(decal.AllowedTiles.Count);
                    break;
                case BiomeMetaLayer meta:
                    hash.Add(4);
                    hash.Add(meta.Template);
                    break;
                default:
                    hash.Add(5);
                    continue;
            }

            hash.Add(layer.Threshold);
            hash.Add(layer.Invert);

            var noise = layer.Noise;
            hash.Add(noise.GetSeed());
            hash.Add(noise.GetFrequency());
            hash.Add(noise.GetNoiseType());
            hash.Add(noise.GetFractalType());
            hash.Add(noise.GetFractalOctaves());
            hash.Add(noise.GetFractalLacunarity());
            hash.Add(noise.GetCellularDistanceFunction());
            hash.Add(noise.GetCellularReturnType());
        }
    }

    private Rgba32 GetTileColor(int tileId)
    {
        if (_tileColors.TryGetValue(tileId, out var color))
            return color;

        color = default;

        if (_tileDef.TryGetDefinition(tileId, out var def)
            && def is ContentTileDefinition { Sprite: { } sprite })
        {
            color = AverageImage(sprite, null, out var coverage);
            if (coverage > 0f)
                color.A = 255;
        }

        _tileColors[tileId] = color;
        return color;
    }

    /// <summary>
    /// the single colour standing in for every entity a layer can pick. Alpha is how much it covers the tile. This could have and should have been alot lazier
    /// </summary>
    private Rgba32 GetLayerEntityColor(List<string> entities)
    {
        if (entities.Count == 0)
            return default;

        float r = 0, g = 0, b = 0, a = 0;

        foreach (var id in entities)
        {
            var c = GetEntityColor(id, 0);
            var weight = c.A / 255f;
            r += c.R * weight;
            g += c.G * weight;
            b += c.B * weight;
            a += weight;
        }

        if (a <= 0f)
            return default;

        return new Rgba32(
            (byte) Math.Clamp(r / a, 0, 255),
            (byte) Math.Clamp(g / a, 0, 255),
            (byte) Math.Clamp(b / a, 0, 255),
            (byte) Math.Clamp(a / entities.Count * 255f, 0, 255));
    }

    private Rgba32 GetEntityColor(string id, int depth)
    {
        if (_entityColors.TryGetValue(id, out var cached))
            return cached;

        var color = ResolveEntityColor(id, depth);
        _entityColors[id] = color;
        return color;
    }

    private Rgba32 ResolveEntityColor(string id, int depth)
    {
        if (!_proto.TryIndex<EntityPrototype>(id, out var proto))
            return default;

        var factory = EntityManager.ComponentFactory;

        if (proto.TryGetComponent<PlanetMapColorComponent>(out var overrides, factory))
        {
            if (overrides.Hidden)
                return default;

            if (overrides.Color is { } c)
                return new Rgba32(c.R, c.G, c.B, c.A);

            if (overrides.ColorFrom is { } from && depth < 4)
                return GetEntityColor(from, depth + 1);
        }

        if (proto.Components.ContainsKey(factory.GetComponentName<MarkerComponent>())
            || proto.Components.ContainsKey(factory.GetComponentName<MobStateComponent>()))
        {
            return default;
        }

        if (!proto.Components.ContainsKey("Sprite") && !proto.Components.ContainsKey("Icon"))
            return default;

        IRsiStateLike icon;
        try
        {
            // GetPrototypeIcon returns a bare Texture for entities with an icon components like liquids,
            // which tells us nothing about where its pixels live. Resolve the icon's RSI state ourselves instead.
            icon = proto.TryGetComponent<IconComponent>(out var iconComp, factory)
                ? _sprite.GetState(iconComp.Icon)
                : _sprite.GetPrototypeIcon(proto);
        }
        catch (Exception e)
        {
            Log.Warning($"Couldn't get planet map icon for {id}: {e.Message}");
            return default;
        }

        if (icon is not RSI.State state || state.RSI.Path == ErrorRsi || state.StateId.Name is not { } stateName)
        {
            Log.Debug($"Planet map has no usable icon for {id}; it will not be drawn.");
            return default;
        }

        var path = state.RSI.Path / $"{stateName}.png";
        var color = AverageImage(path, new Box2i(0, 0, state.Size.X, state.Size.Y), out var coverage);

        // Many sprites are greyscale and tinted in YAML, so apply the sprite colour.
        if (proto.TryGetComponent<SpriteComponent>(out var spriteComp, factory))
        {
            var tint = spriteComp.Color;
            color.R = (byte) Math.Clamp(color.R * tint.R, 0f, 255f);
            color.G = (byte) Math.Clamp(color.G * tint.G, 0f, 255f);
            color.B = (byte) Math.Clamp(color.B * tint.B, 0f, 255f);
        }
        color.A = (byte) Math.Clamp(coverage * 1.5f * 255f, 0f, 255f);
        return color;
    }

    /// <summary>
    /// Average the opaque pixels of a PNG read straight from the resource files.
    /// Don't do GPU texture readback entirely since it seems to stalls the render thread.
    /// </summary>
    private Rgba32 AverageImage(ResPath path, Box2i? region, out float coverage)
    {
        coverage = 0f;

        if (!_res.TryContentFileRead(path, out var stream))
        {
            Log.Warning($"Couldn't find {path} for planet map colours.");
            return default;
        }

        try
        {
            using (stream)
            {
                using var image = Image.Load<Rgba32>(stream);
                var span = image.GetPixelSpan();
                var width = image.Width;

                var box = region ?? new Box2i(0, 0, width, image.Height);
                var right = Math.Min(box.Right, width);
                var top = Math.Min(box.Top, image.Height);

                double r = 0, g = 0, b = 0, weight = 0;
                var total = 0;

                for (var y = Math.Max(box.Bottom, 0); y < top; y++)
                {
                    for (var x = Math.Max(box.Left, 0); x < right; x++)
                    {
                        var px = span[y * width + x];
                        total++;

                        if (px.A < 16)
                            continue;

                        var w = px.A / 255.0;
                        r += px.R * w;
                        g += px.G * w;
                        b += px.B * w;
                        weight += w;
                    }
                }

                if (total == 0 || weight <= 0)
                    return default;

                coverage = (float) (weight / total);
                return new Rgba32((byte) (r / weight), (byte) (g / weight), (byte) (b / weight), 255);
            }
        }
        catch (Exception e)
        {
            Log.Warning($"Couldn't read {path} for planet map colours: {e.Message}");
            return default;
        }
    }

    public readonly record struct ChunkKey(int X, int Y, int Lod);

    public enum TerrainStatus : byte
    {
        /// <summary>
        /// The map has no biome, so there is no terrain to draw.
        /// </summary>
        None,

        /// <summary>
        /// The terrain sampler is being built this frame.
        /// </summary>
        Pending,

        Ready,
    }

    internal enum ChunkState : byte
    {
        Idle,
        Generating,
        Ready,
        Failed,
    }

    public sealed class ChunkEntry
    {
        internal ChunkState State;
        internal OwnedTexture? Texture;
        internal long LastUsed;
        internal long RequestedFrame = -1;
    }

    public sealed class TerrainCache
    {
        internal PlanetTerrainSampler Sampler;
        internal List<IBiomeLayer> LayersRef;
        internal readonly Dictionary<ChunkKey, ChunkEntry> Chunks = new();
        internal readonly StructureLayer Structures = new();
        internal readonly EntityUid MapUid;

        internal int Version;

        internal TerrainCache(EntityUid mapUid, PlanetTerrainSampler sampler, List<IBiomeLayer> layersRef)
        {
            MapUid = mapUid;
            Sampler = sampler;
            LayersRef = layersRef;
        }
    }

    private readonly record struct Request(TerrainCache Cache, ChunkKey Key, ChunkEntry Entry, float Priority)
    {
        public readonly int Version = Cache.Version;
    }

    private readonly record struct Job(TerrainCache Cache, PlanetTerrainSampler Sampler, ChunkKey Key, ChunkEntry Entry, int Version);

    private readonly record struct ChunkResult(TerrainCache Cache, ChunkEntry Entry, int Version, Rgba32[] Pixels, bool Failed);

}

public readonly record struct PlanetDrawOptions(
    float PixelsPerTexel,
    Color TerrainModulate,
    bool DrawStructures,
    Color StructureColor,
    Color BorderColor);
