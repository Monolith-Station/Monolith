/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using System.Threading;
using Content.Server._CE.ZLevels.Gravity;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Robust.Shared.Analyzers;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._CE.ZCollapse;

public sealed partial class CEZCollapseSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CESharedZLevelsSystem _zLevel = default!;
    [Dependency] private ITileDefinitionManager _tileDefMan = default!;
    [Dependency] private SharedDestructibleSystem _destructible = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IComponentFactory _compFactory = default!;

    [Dependency] private EntityQuery<CEGridStabilityComponent> _stabilityQuery = default!;
    [Dependency] private EntityQuery<CEGridStabilityCoreComponent> _coreQuery = default!;
    [Dependency] private EntityQuery<CEGridStabilitySupportComponent> _supportQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private EntityQuery<CEZMapComponent> _zMapQuery = default!;
    [Dependency] private EntityQuery<CEZMapNetworkComponent> _zNetworkQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private EntityQuery<DamageableComponent> _damageableQuery = default!;
    [Dependency] private EntityQuery<MapComponent> _mapCompQuery = default!;

    private const double ZCollapseJobTime = 0.005;
    private static readonly DamageSpecifier CollapseDamage = new()
    {
        DamageDict =
        {
            ["Blunt"] = FixedPoint2.New(1000),
            ["Structural"] = FixedPoint2.New(1000)
        },
    };

    private static readonly EntProtoId CollapseDustEffect = "CEDustTileEffect";
    private static readonly SoundSpecifier CollapseSound = new SoundPathSpecifier("/Audio/Magic/rumble.ogg");
    private const float CollapseDelayMinSeconds = 3f;
    private const float CollapseDelayMaxSeconds = 10f;
    private const int MaxCollapsesPerTick = 8;
    public const int LowestLevelSupportValue = 100;
    private const float DropImpulseMin = 0f;
    private const float DropImpulseMax = 1.5f;

    private readonly JobQueue _jobQueue = new(ZCollapseJobTime);
    private readonly List<(StabilityJob Job, CancellationTokenSource Cts, List<EntityUid> Grids)> _inFlightJobs = new();
    private readonly HashSet<EntityUid> _busyGrids = new();
    private readonly HashSet<EntityUid> _dirtyGrids = new();
    private HashSet<EntityUid> _pendingIndexScan = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
        SubscribeLocalEvent<CEGridStabilityCoreComponent, AnchorStateChangedEvent>(OnCoreAnchorChanged);
        SubscribeLocalEvent<CEGridStabilityCoreComponent, ReAnchorEvent>(OnCoreReAnchor);
        SubscribeLocalEvent<CEGridStabilitySupportComponent, AnchorStateChangedEvent>(OnSupportAnchorChanged);
        SubscribeLocalEvent<CEGridStabilitySupportComponent, ReAnchorEvent>(OnSupportReAnchor);
        SubscribeLocalEvent<CEGridStabilityComponent, TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<CEGridStabilityComponent, MapInitEvent>(OnStabilityMapInit);
        SubscribeLocalEvent<CEGridStabilityComponent, GridSplitEvent>(OnGridSplit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        ProcessPendingIndexScans();
        StartPendingJobs();
        _jobQueue.Process();
        CollectFinishedJobs();
        ProcessPendingCollapses();
        PushDirtySnapshots();
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        foreach (var (_, cts, _) in _inFlightJobs)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _inFlightJobs.Clear();
        _busyGrids.Clear();
        _dirtyGrids.Clear();
        _pendingIndexScan.Clear();
    }

    private void MarkDirty(EntityUid gridUid)
    {
        if (_stabilityQuery.HasComponent(gridUid))
            _dirtyGrids.Add(gridUid);
    }

    private bool IsZCollapseEligible(EntityUid gridUid)
    {
        if (_stabilityQuery.HasComponent(gridUid))
            return true;

        if (!TryGetOwningMap(gridUid, out var mapUid) ||
            !_zMapQuery.TryGetComponent(mapUid, out var zMap) ||
            !_zNetworkQuery.TryGetComponent(zMap.NetworkUid, out var network))
            return false;

        return _zLevel.NetworkHasComponent<CEGridStabilityComponent>((zMap.NetworkUid, network)) ||
               _zLevel.NetworkHasComponent<CEAutoGridGravityComponent>((zMap.NetworkUid, network));
    }

    private bool IsLowestZLevel(EntityUid gridUid)
    {
        if (!TryGetOwningMap(gridUid, out var mapUid) ||
            !_zMapQuery.TryGetComponent(mapUid, out var zMap) ||
            !_zNetworkQuery.TryGetComponent(zMap.NetworkUid, out var network))
            return false;

        return zMap.Depth == network.SortedMin;
    }

    private bool TryGetOwningMap(EntityUid gridUid, out EntityUid mapUid)
    {
        mapUid = EntityUid.Invalid;
        if (!_xformQuery.TryGetComponent(gridUid, out var xform))
            return false;

        mapUid = xform.MapUid ?? EntityUid.Invalid;
        return mapUid.IsValid();
    }

    private bool TryGetParticipatingGrid(EntityUid mapUid, out EntityUid gridUid)
    {
        gridUid = default;
        if (!_mapCompQuery.TryGetComponent(mapUid, out var mapComp))
            return false;

        foreach (var grid in _map.GetAllGrids(mapComp.MapId))
        {
            if (_stabilityQuery.HasComponent(grid.Owner))
            {
                gridUid = grid.Owner;
                return true;
            }
        }

        return false;
    }

    private bool TryGetTileOnGrid(EntityUid gridUid, MapGridComponent grid, Vector2 worldPos, out Vector2i tile)
    {
        tile = default;
        if (!_xformQuery.TryGetComponent(gridUid, out var xform))
            return false;

        tile = _map.TileIndicesFor(gridUid, grid, new MapCoordinates(worldPos, xform.MapID));
        return true;
    }

    private void ProcessPendingIndexScans()
    {
        if (_pendingIndexScan.Count == 0)
            return;

        var toScan = _pendingIndexScan;
        _pendingIndexScan = new HashSet<EntityUid>();
        foreach (var gridUid in toScan)
        {
            if (!_stabilityQuery.TryGetComponent(gridUid, out var comp))
                continue;

            RescanGridIndex(gridUid, comp);
            MarkDirty(gridUid);
        }
    }

    private void RescanGridIndex(EntityUid gridUid, CEGridStabilityComponent comp)
    {
        comp.Cores.Clear();
        comp.Supports.Clear();
        var coreQuery = AllEntityQuery<CEGridStabilityCoreComponent, TransformComponent>();
        while (coreQuery.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == gridUid && xform.Anchored)
                comp.Cores.Add(uid);
        }

        var supportQuery = AllEntityQuery<CEGridStabilitySupportComponent, TransformComponent>();
        while (supportQuery.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == gridUid && xform.Anchored)
                comp.Supports.Add(uid);
        }
    }

    private List<EntityUid> GetColumn(EntityUid startGrid, Func<EntityUid, bool> participates)
    {
        bool TryGetNeighborGrid(EntityUid mapUid, out EntityUid gridUid)
        {
            gridUid = default;
            if (!_mapCompQuery.TryGetComponent(mapUid, out var mapComp))
                return false;

            foreach (var candidate in _map.GetAllGrids(mapComp.MapId))
            {
                if (!participates(candidate.Owner))
                    continue;

                gridUid = candidate.Owner;
                return true;
            }

            return false;
        }

        var above = new List<EntityUid>();
        if (TryGetOwningMap(startGrid, out var currentMap))
        {
            while (_zMapQuery.TryGetComponent(currentMap, out var zMap) &&
                   _zLevel.TryMapUp((currentMap, zMap), out var upMap) &&
                   TryGetNeighborGrid(upMap.Owner, out var upGrid))
            {
                above.Add(upGrid);
                currentMap = upMap.Owner;
            }
        }

        var below = new List<EntityUid>();
        if (TryGetOwningMap(startGrid, out currentMap))
        {
            while (_zMapQuery.TryGetComponent(currentMap, out var zMap) &&
                   _zLevel.TryMapDown((currentMap, zMap), out var downMap) &&
                   TryGetNeighborGrid(downMap.Owner, out var downGrid))
            {
                below.Add(downGrid);
                currentMap = downMap.Owner;
            }
        }

        above.Reverse();
        above.Add(startGrid);
        above.AddRange(below);
        return above;
    }

    private void StartPendingJobs()
    {
        if (_dirtyGrids.Count == 0)
            return;

        var toStart = new List<EntityUid>(_dirtyGrids);
        foreach (var gridUid in toStart)
        {
            if (!_dirtyGrids.Contains(gridUid) || _busyGrids.Contains(gridUid))
                continue;

            var column = GetColumn(gridUid, _stabilityQuery.HasComponent);
            var columnBusy = false;
            foreach (var g in column)
            {
                if (_busyGrids.Contains(g))
                {
                    columnBusy = true;
                    break;
                }
            }

            if (columnBusy)
                continue;

            foreach (var g in column)
                _dirtyGrids.Remove(g);
            StartJob(column);
        }
    }

    private void StartJob(List<EntityUid> column)
    {
        var aboveOf = new Dictionary<EntityUid, EntityUid>();
        foreach (var gridUid in column)
        {
            if (TryGetOwningMap(gridUid, out var mapUid) &&
                _zMapQuery.TryGetComponent(mapUid, out var zMap) &&
                _zLevel.TryMapUp((mapUid, zMap), out var aboveMap) &&
                TryGetParticipatingGrid(aboveMap.Owner, out var aboveGrid))
            {
                aboveOf[gridUid] = aboveGrid;
            }
        }

        var liveNodes = new HashSet<(EntityUid, Vector2i)>();
        var coreSeeds = new List<(EntityUid, Vector2i, int)>();
        var bridges = new Dictionary<(EntityUid, Vector2i), List<((EntityUid Grid, Vector2i Tile) Node, int Strength, int Loss)>>();

        foreach (var gridUid in column)
        {
            if (!_stabilityQuery.TryGetComponent(gridUid, out var comp) || !_gridQuery.TryGetComponent(gridUid, out var grid))
                continue;

            var supportLowestLevel = comp.SupportLowestLevel && IsLowestZLevel(gridUid);
            var tileEnumerator = _map.GetAllTilesEnumerator(gridUid, grid);
            while (tileEnumerator.MoveNext(out var tileRef))
            {
                var tile = tileRef.Value.GridIndices;
                liveNodes.Add((gridUid, tile));
                if (supportLowestLevel)
                    coreSeeds.Add((gridUid, tile, LowestLevelSupportValue));
            }

            foreach (var coreUid in comp.Cores)
            {
                if (!_coreQuery.TryGetComponent(coreUid, out var core) || !_xformQuery.TryGetComponent(coreUid, out var xform))
                    continue;

                coreSeeds.Add((gridUid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates), core.LevitationForce));
            }

            if (!aboveOf.TryGetValue(gridUid, out var aboveGrid) || !_gridQuery.TryGetComponent(aboveGrid, out var aboveGridComp))
                continue;

            foreach (var supportUid in comp.Supports)
            {
                if (!_supportQuery.TryGetComponent(supportUid, out var support) || !_xformQuery.TryGetComponent(supportUid, out var xform))
                    continue;
                if (!TryGetTileOnGrid(aboveGrid, aboveGridComp, _transform.GetWorldPosition(xform), out var aboveTile))
                    continue;

                var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
                AddBridge(bridges, (gridUid, tile), (aboveGrid, aboveTile), support.SupportStrength, support.TransferLoss);
            }
        }

        var cts = new CancellationTokenSource();
        var job = new StabilityJob(ZCollapseJobTime, liveNodes, coreSeeds, bridges, cts.Token);
        foreach (var gridUid in column)
            _busyGrids.Add(gridUid);

        _inFlightJobs.Add((job, cts, column));
        _jobQueue.EnqueueJob(job);
    }

    private static void AddBridge(
        Dictionary<(EntityUid, Vector2i), List<((EntityUid Grid, Vector2i Tile) Node, int Strength, int Loss)>> bridges,
        (EntityUid, Vector2i) a,
        (EntityUid, Vector2i) b,
        int strength,
        int loss)
    {
        if (!bridges.TryGetValue(a, out var listA))
            bridges[a] = listA = new List<((EntityUid, Vector2i), int, int)>();
        listA.Add((b, strength, loss));
        if (!bridges.TryGetValue(b, out var listB))
            bridges[b] = listB = new List<((EntityUid, Vector2i), int, int)>();
        listB.Add((a, strength, loss));
    }

    private void CollectFinishedJobs()
    {
        if (_inFlightJobs.Count == 0)
            return;

        List<int>? finishedIndices = null;
        for (var i = 0; i < _inFlightJobs.Count; i++)
        {
            if (_inFlightJobs[i].Job.Status != JobStatus.Finished)
                continue;
            finishedIndices ??= new List<int>();
            finishedIndices.Add(i);
        }

        if (finishedIndices == null)
            return;

        for (var i = finishedIndices.Count - 1; i >= 0; i--)
        {
            var idx = finishedIndices[i];
            var (job, cts, grids) = _inFlightJobs[idx];
            _inFlightJobs.RemoveAt(idx);
            cts.Dispose();
            foreach (var g in grids)
                _busyGrids.Remove(g);
            ApplyJobResult(grids, job);
        }
    }

    private void ApplyJobResult(List<EntityUid> grids, StabilityJob job)
    {
        if (job.Exception != null)
        {
            Log.Error($"ZCollapse: stability job faulted: {job.Exception}");
            return;
        }

        var result = job.Result ?? new Dictionary<(EntityUid, Vector2i), int>();
        var stabilityByGrid = new Dictionary<EntityUid, Dictionary<Vector2i, int>>();
        foreach (var ((nodeGrid, tile), value) in result)
        {
            if (!stabilityByGrid.TryGetValue(nodeGrid, out var dict))
                stabilityByGrid[nodeGrid] = dict = new Dictionary<Vector2i, int>();
            dict[tile] = value;
        }

        var liveByGrid = new Dictionary<EntityUid, HashSet<Vector2i>>();
        foreach (var (nodeGrid, tile) in job.LiveNodes)
        {
            if (!liveByGrid.TryGetValue(nodeGrid, out var set))
                liveByGrid[nodeGrid] = set = new HashSet<Vector2i>();
            set.Add(tile);
        }

        foreach (var gridUid in grids)
        {
            if (!_stabilityQuery.TryGetComponent(gridUid, out var comp) || !_gridQuery.TryGetComponent(gridUid, out var grid))
                continue;

            var newStability = stabilityByGrid.GetValueOrDefault(gridUid) ?? new Dictionary<Vector2i, int>();
            var liveTiles = liveByGrid.GetValueOrDefault(gridUid) ?? new HashSet<Vector2i>();
            ScheduleCollapsingTiles(gridUid, grid, comp, liveTiles, newStability);
            comp.Stability.Clear();
            foreach (var (tile, value) in newStability)
                comp.Stability[tile] = value;
            _debugDirtyGrids.Add(gridUid);
        }
    }

    private void ScheduleCollapsingTiles(EntityUid gridUid, MapGridComponent grid, CEGridStabilityComponent comp, IReadOnlySet<Vector2i> liveTiles, Dictionary<Vector2i, int> newStability)
    {
        if (!_map.IsInitialized(Transform(gridUid).MapUid))
            return;

        foreach (var tile in liveTiles)
        {
            if (newStability.ContainsKey(tile))
            {
                comp.PendingCollapses.Remove(tile);
                continue;
            }

            if (comp.PendingCollapses.ContainsKey(tile))
                continue;
            if (!_map.TryGetTile(grid, tile, out var currentTile) || currentTile.IsEmpty)
                continue;
            if (_tileDefMan[currentTile.TypeId] is ContentTileDefinition { Indestructible: true })
                continue;

            var coords = _map.GridTileToLocal(gridUid, grid, tile);
            SpawnAtPosition(CollapseDustEffect, coords);
            _audio.PlayPvs(CollapseSound, coords);
            comp.PendingCollapses[tile] = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(CollapseDelayMinSeconds, CollapseDelayMaxSeconds));
        }
    }

    private void ProcessPendingCollapses()
    {
        var budget = MaxCollapsesPerTick;
        var query = AllEntityQuery<CEGridStabilityComponent, MapGridComponent>();
        while (budget > 0 && query.MoveNext(out var gridUid, out var comp, out var grid))
        {
            if (comp.PendingCollapses.Count == 0)
                continue;

            List<Vector2i>? due = null;
            foreach (var (tile, collapseAt) in comp.PendingCollapses)
            {
                if (collapseAt > _timing.CurTime)
                    continue;
                due ??= new List<Vector2i>();
                due.Add(tile);
                if (due.Count >= budget)
                    break;
            }

            if (due == null)
                continue;
            foreach (var tile in due)
            {
                comp.PendingCollapses.Remove(tile);
                CollapseTile(gridUid, grid, tile);
            }
            budget -= due.Count;
        }
    }

    private void CollapseTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        if (!_map.TryGetTile(grid, tile, out var currentTile) || currentTile.IsEmpty)
            return;

        var tileDef = (ContentTileDefinition)_tileDefMan[currentTile.TypeId];
        if (tileDef.Indestructible)
            return;

        var collapseCoords = _map.GridTileToLocal(gridUid, grid, tile);
        SpawnAtPosition(CollapseDustEffect, collapseCoords);
        _audio.PlayPvs(CollapseSound, collapseCoords);
        DestroyAnchoredEntities(gridUid, grid, tile);
        DropTileItemBelow(gridUid, tile, tileDef);
        var nextTile = tileDef.BaseTurf is { } baseTurf ? new Tile(_tileDefMan[baseTurf].TileId) : Tile.Empty;
        _map.SetTile(gridUid, grid, tile, nextTile);
    }

    private void DropTileItemBelow(EntityUid gridUid, Vector2i tile, ContentTileDefinition tileDef)
    {
        var itemProto = tileDef.ItemDropPrototypeName;
        if (itemProto == null)
            return;

        if (!TryGetOwningMap(gridUid, out var mapUid) ||
            !_zMapQuery.TryGetComponent(mapUid, out var zMap) ||
            !_zLevel.TryMapDown((mapUid, zMap), out var belowMap) ||
            !TryGetParticipatingGrid(belowMap.Owner, out var belowGridUid) ||
            !_gridQuery.TryGetComponent(belowGridUid, out var belowGrid))
            return;

        var item = Spawn(itemProto, _map.GridTileToLocal(belowGridUid, belowGrid, tile));
        _zLevel.SetZPosition(item, 0.9f);
        Transform(item).LocalRotation = _random.NextAngle();
        if (TryComp<PhysicsComponent>(item, out var physics))
            _physics.ApplyLinearImpulse(item, _random.NextVector2(DropImpulseMin, DropImpulseMax), body: physics);
    }

    private void DestroyAnchoredEntities(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var enumerator = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        List<EntityUid>? anchored = null;
        while (enumerator.MoveNext(out var ent))
        {
            anchored ??= new List<EntityUid>();
            anchored.Add(ent.Value);
        }

        if (anchored == null)
            return;
        foreach (var uid in anchored)
        {
            if (_damageableQuery.TryGetComponent(uid, out var damageable))
                _damageable.TryChangeDamage(uid, CollapseDamage, ignoreResistances: true, damageable: damageable, ignoreGlobalModifiers: true);
            _destructible.DestroyEntity(uid);
        }
    }
}
