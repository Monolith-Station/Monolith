/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using Content.Shared._CE.ZCollapse.Events;
using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server._CE.ZCollapse;

public sealed partial class CEZCollapseSystem
{
    private readonly HashSet<ICommonSession> _debugSessions = new();
    private readonly HashSet<EntityUid> _debugDirtyGrids = new();
    private static readonly TimeSpan PreviewRefreshInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextPreviewRefresh = TimeSpan.Zero;

    public void ToggleDebugView(ICommonSession session)
    {
        var isEnabled = _debugSessions.Add(session);
        if (!isEnabled)
            _debugSessions.Remove(session);

        RaiseNetworkEvent(new CEZCollapseOverlayToggledEvent(isEnabled), session.Channel);
        if (isEnabled)
            SendFullSnapshot(session);
    }

    private void SendFullSnapshot(ICommonSession session)
    {
        var dict = new Dictionary<NetEntity, Dictionary<Vector2i, int>>();
        var query = AllEntityQuery<CEGridStabilityComponent>();
        while (query.MoveNext(out var gridUid, out var comp))
        {
            if (_gridQuery.TryGetComponent(gridUid, out var grid))
                dict[GetNetEntity(gridUid)] = BuildOverlayTiles(gridUid, grid, comp.Stability);
        }

        AddPreviewSnapshots(dict);
        RaiseNetworkEvent(new CEZCollapseOverlaySnapshotEvent(dict), session);
    }

    private void PushDirtySnapshots()
    {
        if (_debugSessions.Count == 0)
        {
            _debugDirtyGrids.Clear();
            return;
        }

        var dict = new Dictionary<NetEntity, Dictionary<Vector2i, int>>();
        foreach (var gridUid in _debugDirtyGrids)
        {
            if (_stabilityQuery.TryGetComponent(gridUid, out var comp) && _gridQuery.TryGetComponent(gridUid, out var grid))
                dict[GetNetEntity(gridUid)] = BuildOverlayTiles(gridUid, grid, comp.Stability);
        }

        _debugDirtyGrids.Clear();
        if (_timing.CurTime >= _nextPreviewRefresh)
        {
            _nextPreviewRefresh = _timing.CurTime + PreviewRefreshInterval;
            AddPreviewSnapshots(dict);
        }

        if (dict.Count == 0)
            return;

        var ev = new CEZCollapseOverlaySnapshotEvent(dict);
        foreach (var session in _debugSessions.ToArray())
        {
            if (session.Status != SessionStatus.InGame)
                _debugSessions.Remove(session);
            else
                RaiseNetworkEvent(ev, session);
        }
    }

    private void AddPreviewSnapshots(Dictionary<NetEntity, Dictionary<Vector2i, int>> dict)
    {
        var visited = new HashSet<EntityUid>();
        var query = AllEntityQuery<CEZMapComponent, MapComponent>();
        while (query.MoveNext(out _, out _, out var mapComp))
        {
            foreach (var grid in _map.GetAllGrids(mapComp.MapId))
            {
                var gridUid = grid.Owner;
                if (_stabilityQuery.HasComponent(gridUid) || visited.Contains(gridUid) || !IsZCollapseEligible(gridUid))
                    continue;

                var column = GetColumn(gridUid, IsZCollapseEligible);
                foreach (var g in column)
                    visited.Add(g);

                var stabilityByGrid = ComputePreviewColumn(column);
                foreach (var g in column)
                {
                    if (!_gridQuery.TryGetComponent(g, out var gridComp))
                        continue;
                    var stability = stabilityByGrid.GetValueOrDefault(g) ?? new Dictionary<Vector2i, int>();
                    dict[GetNetEntity(g)] = BuildOverlayTiles(g, gridComp, stability);
                }
            }
        }
    }

    private Dictionary<EntityUid, Dictionary<Vector2i, int>> ComputePreviewColumn(List<EntityUid> column)
    {
        var columnSet = new HashSet<EntityUid>(column);
        var aboveOf = new Dictionary<EntityUid, EntityUid>();
        var liveNodes = new HashSet<(EntityUid, Vector2i)>();
        var gridsByUid = new Dictionary<EntityUid, MapGridComponent>();

        foreach (var gridUid in column)
        {
            if (TryGetOwningMap(gridUid, out var mapUid) &&
                _zMapQuery.TryGetComponent(mapUid, out var zMap) &&
                _zLevel.TryMapUp((mapUid, zMap), out var aboveMap) &&
                _mapCompQuery.TryGetComponent(aboveMap.Owner, out var aboveMapComp))
            {
                foreach (var candidate in _map.GetAllGrids(aboveMapComp.MapId))
                {
                    if (!columnSet.Contains(candidate.Owner) || !IsZCollapseEligible(candidate.Owner))
                        continue;

                    aboveOf[gridUid] = candidate.Owner;
                    break;
                }
            }

            if (!_gridQuery.TryGetComponent(gridUid, out var grid))
                continue;
            gridsByUid[gridUid] = grid;
            var tileEnumerator = _map.GetAllTilesEnumerator(gridUid, grid);
            while (tileEnumerator.MoveNext(out var tileRef))
                liveNodes.Add((gridUid, tileRef.Value.GridIndices));
        }

        var coreSeeds = new List<(EntityUid, Vector2i, int)>();
        var coreQuery = AllEntityQuery<CEGridStabilityCoreComponent, TransformComponent>();
        while (coreQuery.MoveNext(out _, out var core, out var xform))
        {
            if (xform.GridUid is not { } gridUid || !xform.Anchored || !gridsByUid.TryGetValue(gridUid, out var grid))
                continue;
            coreSeeds.Add((gridUid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates), core.LevitationForce));
        }

        var bridges = new Dictionary<(EntityUid, Vector2i), List<((EntityUid Grid, Vector2i Tile) Node, int Strength, int Loss)>>();
        var supportQuery = AllEntityQuery<CEGridStabilitySupportComponent, TransformComponent>();
        while (supportQuery.MoveNext(out _, out var support, out var xform))
        {
            if (xform.GridUid is not { } gridUid || !xform.Anchored || !gridsByUid.TryGetValue(gridUid, out var grid))
                continue;
            if (!aboveOf.TryGetValue(gridUid, out var aboveGrid) || !gridsByUid.TryGetValue(aboveGrid, out var aboveGridComp))
                continue;
            if (!TryGetTileOnGrid(aboveGrid, aboveGridComp, _transform.GetWorldPosition(xform), out var aboveTile))
                continue;

            var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            AddBridge(bridges, (gridUid, tile), (aboveGrid, aboveTile), support.SupportStrength, support.TransferLoss);
        }

        var stability = new Dictionary<(EntityUid, Vector2i), int>();
        var queue = new Queue<((EntityUid Grid, Vector2i Tile) Node, int Value)>();
        CEStabilityFloodFill.SeedCores(stability, queue, liveNodes, coreSeeds);
        CEStabilityFloodFill.Process(queue, stability, liveNodes, bridges);

        var byGrid = new Dictionary<EntityUid, Dictionary<Vector2i, int>>();
        foreach (var ((nodeGrid, tile), value) in stability)
        {
            if (!byGrid.TryGetValue(nodeGrid, out var d))
                byGrid[nodeGrid] = d = new Dictionary<Vector2i, int>();
            d[tile] = value;
        }

        return byGrid;
    }

    private Dictionary<Vector2i, int> BuildOverlayTiles(EntityUid gridUid, MapGridComponent grid, Dictionary<Vector2i, int> stability)
    {
        var result = new Dictionary<Vector2i, int>(stability);
        var enumerator = _map.GetAllTilesEnumerator(gridUid, grid);
        while (enumerator.MoveNext(out var tileRef))
            result.TryAdd(tileRef.Value.GridIndices, 0);
        return result;
    }
}
