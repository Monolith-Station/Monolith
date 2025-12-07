// Mono - Refactored into smaller subsystems
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Decals;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared.Ghost;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Tag;
using Microsoft.Extensions.ObjectPool;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Threading;
using Robust.Shared.Utility;

namespace Content.Server.Parallax;

public sealed partial class BiomeSystem : SharedBiomeSystem
{
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IParallelManager _parallel = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ShuttleSystem _shuttles = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private EntityQuery<BiomeComponent> _biomeQuery;
    private EntityQuery<FixturesComponent> _fixturesQuery;
    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private readonly HashSet<EntityUid> _handledEntities = new();
    private const float DefaultLoadRange = 16f;
    private float _loadRange = DefaultLoadRange;
    private static readonly ProtoId<TagPrototype> AllowBiomeLoadingTag = "AllowBiomeLoading";

    private ObjectPool<HashSet<Vector2i>> _tilePool =
        new DefaultObjectPool<HashSet<Vector2i>>(new SetPolicy<Vector2i>(), 256);

    private float _updateTimer = 0f;
    private const float UpdateInterval = 1f / 10f;

    private float _unloadTimer = 0f;
    private const float UnloadInterval = 10f;

    /// <summary>
    /// Load area for chunks containing tiles, decals etc.
    /// </summary>
    private Box2 _loadArea = new(-DefaultLoadRange, -DefaultLoadRange, DefaultLoadRange, DefaultLoadRange);

    /// <summary>
    /// Stores the chunks active for this tick temporarily.
    /// </summary>
    private readonly Dictionary<BiomeComponent, HashSet<Vector2i>> _activeChunks = new();

    private readonly Dictionary<BiomeComponent,
        Dictionary<string, HashSet<Vector2i>>> _markerChunks = new();

    public override void Initialize()
    {
        base.Initialize();
        Log.Level = LogLevel.Debug;
        _biomeQuery = GetEntityQuery<BiomeComponent>();
        _fixturesQuery = GetEntityQuery<FixturesComponent>();
        _ghostQuery = GetEntityQuery<GhostComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        SubscribeLocalEvent<BiomeComponent, MapInitEvent>(OnBiomeMapInit);
        SubscribeLocalEvent<FTLStartedEvent>(OnFTLStarted);
        SubscribeLocalEvent<ShuttleFlattenEvent>(OnShuttleFlatten);
        Subs.CVar(_configManager, CVars.NetMaxUpdateRange, SetLoadRange, true);
        InitializeChunkLoader();
        InitializeMarkerProcessor();
        InitializePlayerTracker();
        InitializeConfigManager();
        InitializePlanetSetup();
        InitializeCommands();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(ProtoReload);
    }

    private void OnFTLStarted(ref FTLStartedEvent ev)
    {
        var targetMap = _transform.ToMapCoordinates(ev.TargetCoordinates);
        var targetMapUid = _mapSystem.GetMapOrInvalid(targetMap.MapId);

        if (!TryComp<BiomeComponent>(targetMapUid, out var biome))
            return;

        var preloadArea = new Vector2(32f, 32f);
        var targetArea = new Box2(targetMap.Position - preloadArea, targetMap.Position + preloadArea);
        Preload(targetMapUid, biome, targetArea);
    }

    private void OnShuttleFlatten(ref ShuttleFlattenEvent ev)
    {
        if (!TryComp<BiomeComponent>(ev.MapUid, out var biome) ||
            !TryComp<MapGridComponent>(ev.MapUid, out var grid))
        {
            return;
        }

        var tiles = new List<(Vector2i Index, Tile Tile)>();

        foreach (var aabb in ev.AABBs)
        {
            for (var x = Math.Floor(aabb.Left); x <= Math.Ceiling(aabb.Right); x++)
            {
                for (var y = Math.Floor(aabb.Bottom); y <= Math.Ceiling(aabb.Top); y++)
                {
                    var index = new Vector2i((int)x, (int)y);
                    var chunk = SharedMapSystem.GetChunkIndices(index, ChunkSize);

                    var mod = biome.ModifiedTiles.GetOrNew(chunk * ChunkSize);

                    if (!mod.Add(index) || !TryGetBiomeTile(index, biome.Layers, biome.Seed, (ev.MapUid, grid), out var tile))
                        continue;

                    // If we flag it as modified then the tile is never set so need to do it ourselves.
                    tiles.Add((index, tile.Value));
                }
            }
        }

        _mapSystem.SetTiles(ev.MapUid, grid, tiles);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Rate limit according to update interval instead of every frame
        _updateTimer += frameTime;
        if (_updateTimer < UpdateInterval)
            return;
        _updateTimer = 0f;

        var biomes = AllEntityQuery<BiomeComponent>();

        while (biomes.MoveNext(out var biome))
        {
            if (biome.LifeStage < ComponentLifeStage.Running)
                continue;

            _activeChunks.Add(biome, _tilePool.Get());
            _markerChunks.GetOrNew(biome);
        }

        ProcessPlayerChunkRequests();

        // Early exit if no players around chunk
        if (_handledEntities.Count == 0)
        {
            CleanupUpdateCycle();
            return;
        }

        var loadBiomes = AllEntityQuery<BiomeComponent, MapGridComponent>();

        _unloadTimer += frameTime;
        var shouldUnload = _unloadTimer > UnloadInterval;

        while (loadBiomes.MoveNext(out var gridUid, out var biome, out var grid))
        {
            // If not MapInit don't run it.
            if (biome.LifeStage < ComponentLifeStage.Running)
                continue;

            if (!biome.Enabled)
                continue;

            // Only process biomes with active chunks
            if (!_activeChunks.ContainsKey(biome))
                continue;

            LoadChunks(biome, gridUid, grid, biome.Seed);

            if (shouldUnload)
                UnloadChunks(biome, gridUid, grid, biome.Seed);
        }

        if (shouldUnload)
            _unloadTimer = 0f;

        CleanupUpdateCycle();
    }

    private void CleanupUpdateCycle()
    {
        _handledEntities.Clear();

        foreach (var tiles in _activeChunks.Values)
        {
            _tilePool.Return(tiles);
        }

        _activeChunks.Clear();
        _markerChunks.Clear();
    }

    /// <summary>
    /// Loads all of the chunks for a particular biome, as well as handle any marker chunks.
    /// </summary>
    private void LoadChunks(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        int seed)
    {
        BuildMarkerChunks(component, gridUid, grid, seed);

        var active = _activeChunks[component];

        foreach (var chunk in active)
        {
            LoadChunkMarkers(component, gridUid, grid, chunk, seed);

            if (!component.LoadedChunks.Add(chunk))
                continue;

            // Load NOW!
            LoadChunk(component, gridUid, grid, chunk, seed);
        }
    }
}
