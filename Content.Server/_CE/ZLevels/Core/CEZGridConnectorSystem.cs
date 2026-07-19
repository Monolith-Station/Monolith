/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.Map;

namespace Content.Server._CE.ZLevels.Core;

//WARNING: This file is vibecoded. It WORKS, but i dunno how that works - and we need investigate that and rewrite to more propriate code human style.

/// <summary>
/// Universal z-grid network recalculator driven by <see cref="CEZGridConnectorComponent"/>.
/// Sets a dirty flag on any topology event and runs a single recalculation pass per dirty cycle.
/// Network membership is always fully derived from the set of active connector entities.
/// </summary>
public sealed partial class CEZGridConnectorSystem : EntitySystem
{
    [Dependency] private CEZLevelsSystem _zLevels = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;

    [Dependency] private EntityQuery<CEZGridComponent> _zgridQuery = default!;
    [Dependency] private EntityQuery<CEZGridNetworkComponent> _zgridNetworkQuery = default!;
    [Dependency] private EntityQuery<CEZMapComponent> _zMapQuery = default!;

    private bool _dirty;

    // Reusable scratch buffers — recalc runs at most once per tick on a single thread,
    // so we clear and reuse rather than allocating fresh collections each pass.
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _adj = new();
    private readonly List<HashSet<EntityUid>> _components = new();
    private readonly Stack<HashSet<EntityUid>> _setPool = new();
    private readonly HashSet<EntityUid> _visited = new();
    private readonly Queue<EntityUid> _bfsQueue = new();
    private readonly Dictionary<EntityUid, EntityUid> _gridToTargetNet = new();
    private readonly HashSet<EntityUid> _claimedNets = new();
    private readonly List<EntityUid> _removeBuffer = new();

    private HashSet<EntityUid> RentSet()
    {
        return _setPool.Count > 0 ? _setPool.Pop() : new HashSet<EntityUid>();
    }

    private void ReturnSet(HashSet<EntityUid> set)
    {
        set.Clear();
        _setPool.Push(set);
    }

    /// <summary>Schedules a network recalculation on the next tick.</summary>
    public void MarkDirty()
    {
        _dirty = true;
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZGridConnectorComponent, MapInitEvent>(OnConnectorMapInit);
        SubscribeLocalEvent<CEZGridConnectorComponent, AnchorStateChangedEvent>(OnConnectorAnchorChanged);
        SubscribeLocalEvent<CEZGridConnectorComponent, EntityTerminatingEvent>(OnConnectorTerminating);

        SubscribeLocalEvent<CEZGridComponent, EntityTerminatingEvent>(OnGridTerminating);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);

        SubscribeLocalEvent<CEZGridNetworkComponent, ComponentShutdown>(OnGridNetworkShutdown);
    }

    private void OnConnectorMapInit(Entity<CEZGridConnectorComponent> ent, ref MapInitEvent args)
    {
        _dirty = true;
    }

    private void OnConnectorAnchorChanged(Entity<CEZGridConnectorComponent> ent, ref AnchorStateChangedEvent args)
    {
        _dirty = true;
    }

    private void OnConnectorTerminating(Entity<CEZGridConnectorComponent> ent, ref EntityTerminatingEvent args)
    {
        _dirty = true;
    }

    private void OnGridTerminating(Entity<CEZGridComponent> ent, ref EntityTerminatingEvent args)
    {
        _dirty = true;
    }

    private void OnTileChanged(ref TileChangedEvent ev)
    {
        _dirty = true;
    }

    private void OnGridSplit(ref GridSplitEvent ev)
    {
        _dirty = true;
    }

    private void OnGridNetworkShutdown(Entity<CEZGridNetworkComponent> ent, ref ComponentShutdown args)
    {
        foreach (var grid in ent.Comp.Grids.ToList())
        {
            if (!_zgridQuery.TryComp(grid, out var gc) || gc.Network != ent.Owner)
                continue;
            _zLevels.TryRemoveGridFromNetwork(grid);
        }
    }

    public override void Update(float frameTime)
    {
        if (!_dirty)
            return;

        _dirty = false;
        RecalculateGridNetworks();
    }

    /// <summary>
    /// Reconciles live network membership with the desired connected components by applying the
    /// minimal delta: survivors keep their pose, only joined grids fire <see cref="CEGridAddedIntoZNetworkEvent"/>
    /// and only departed grids fire <see cref="CEGridRemovedFromZNetworkEvent"/>. Networks are reused (never
    /// torn down and rebuilt) so unrelated members are never re-snapped.
    /// </summary>
    private void RecalculateGridNetworks()
    {
        ComputeDesiredComponents();

        // Assign each desired component a target network, reusing the existing network with the
        // largest overlap (so its members stay put). Each existing network is claimed at most once.
        _claimedNets.Clear();
        _gridToTargetNet.Clear();

        foreach (var component in _components)
        {
            var target = PickSurvivorNetwork(component);
            if (!target.IsValid())
                target = _zLevels.CreateGridNetwork().Owner;

            _claimedNets.Add(target);
            foreach (var grid in component)
            {
                _gridToTargetNet[grid] = target;
            }
        }

        // Removals first: any grid whose live network is not its target leaves (raises Unlinked).
        // This frees grids before they are re-added to a different network during a merge/split.
        _removeBuffer.Clear();
        var nq = EntityQueryEnumerator<CEZGridNetworkComponent>();
        while (nq.MoveNext(out var netUid, out var nc))
        {
            foreach (var grid in nc.Grids)
            {
                if (!_gridToTargetNet.TryGetValue(grid, out var target) || target != netUid)
                    _removeBuffer.Add(grid);
            }
        }

        foreach (var grid in _removeBuffer)
        {
            _zLevels.TryRemoveGridFromNetwork(grid);
        }

        // Additions: place each grid into its target network if it isn't there already.
        foreach (var (grid, target) in _gridToTargetNet)
        {
            if (!_zgridNetworkQuery.TryComp(target, out var nc))
                continue;
            if (!nc.Grids.Contains(grid))
                _zLevels.TryAddGridToNetwork((target, nc), grid);
        }

        // Reclaim component sets for the next pass.
        foreach (var component in _components)
        {
            ReturnSet(component);
        }

        _components.Clear();
    }

    /// <summary>Existing network sharing the most grids with <paramref name="component"/>, or Invalid.</summary>
    private EntityUid PickSurvivorNetwork(HashSet<EntityUid> component)
    {
        var best = EntityUid.Invalid;
        var bestOverlap = 0;

        var nq = EntityQueryEnumerator<CEZGridNetworkComponent>();
        while (nq.MoveNext(out var netUid, out var nc))
        {
            if (_claimedNets.Contains(netUid))
                continue;

            var overlap = 0;
            foreach (var grid in nc.Grids)
            {
                if (component.Contains(grid))
                    overlap++;
            }

            if (overlap <= bestOverlap)
                continue;
            bestOverlap = overlap;
            best = netUid;
        }

        return best;
    }

    private void ComputeDesiredComponents()
    {
        _adj.Clear();

        var query = EntityQueryEnumerator<CEZGridConnectorComponent, TransformComponent>();
        while (query.MoveNext(out var connectorUid, out _, out var xform))
        {
            if (!xform.Anchored || xform.GridUid == null || xform.MapUid == null)
                continue;

            if (xform.ParentUid == xform.MapUid)
                continue; //We do not support connecting grid to planet maps right now

            var lowerGridUid = xform.GridUid.Value;
            if (!_zMapQuery.TryComp(xform.MapUid.Value, out var zMap))
                continue;
            if (!_zLevels.TryMapUp((xform.MapUid.Value, zMap), out var aboveMap))
                continue;

            var worldPos = _transform.GetWorldPosition(connectorUid);
            if (!_mapManager.TryFindGridAt(aboveMap.Owner, worldPos, out var upperGridUid, out var upperGrid))
                continue;
            if (upperGridUid == lowerGridUid)
                continue;

            // TryFindGridAt matches by AABB — verify the tile at this position actually exists
            if (!_mapSystem.TryGetTileRef(upperGridUid, upperGrid, worldPos, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            if (!_adj.TryGetValue(lowerGridUid, out var lowerNeighbors))
                _adj[lowerGridUid] = lowerNeighbors = RentSet();

            if (!_adj.TryGetValue(upperGridUid, out var upperNeighbors))
                _adj[upperGridUid] = upperNeighbors = RentSet();

            lowerNeighbors.Add(upperGridUid);
            upperNeighbors.Add(lowerGridUid);
        }

        // BFS connected components.
        _visited.Clear();

        foreach (var start in _adj.Keys)
        {
            if (!_visited.Add(start))
                continue;

            var comp = RentSet();
            _bfsQueue.Clear();
            _bfsQueue.Enqueue(start);

            while (_bfsQueue.Count > 0)
            {
                var node = _bfsQueue.Dequeue();
                comp.Add(node);
                if (!_adj.TryGetValue(node, out var neighbors))
                    continue;

                foreach (var neighbor in neighbors)
                {
                    if (_visited.Add(neighbor))
                        _bfsQueue.Enqueue(neighbor);
                }
            }

            _components.Add(comp);
        }

        // Return adjacency neighbor sets to the pool (component sets are released after reconciliation).
        foreach (var neighbors in _adj.Values)
        {
            ReturnSet(neighbors);
        }

        _adj.Clear();
    }
}
