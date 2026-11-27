using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZCollapse;

public sealed partial class CEZCollapseSystem
{
    public bool? CanSupportTile(EntityUid grid, Vector2i cell) =>
        GetConstructionSupport(grid, [cell])?.Printable.Contains(cell);

    public (HashSet<Vector2i> Printable, HashSet<Vector2i> Projected)? GetConstructionSupport(
        EntityUid grid, HashSet<Vector2i> cells)
    {
        if (!_stabilityQuery.TryGetComponent(grid, out var stability) || !_gridQuery.TryGetComponent(grid, out var gridComp))
            return (cells, cells);

        if (stability.SupportLowestLevel && IsLowestZLevel(grid))
            return (cells, cells);

        if (IsPending(grid))
            return null;

        EntityUid? below = null;
        if (TryGetOwningMap(grid, out var map) &&
            _zLevel.TryMapDown(map, out var lowerMap) &&
            TryGetParticipatingGrid(lowerMap.Owner, out var lower))
        {
            if (IsPending(lower))
                return null;
            below = lower;
        }

        var strengths = new Dictionary<Vector2i, int>();
        foreach (var cell in cells)
        {
            var strength = stability.Stability.GetValueOrDefault(cell);
            foreach (var offset in CEStabilityFloodFill.CardinalOffsets)
                strength = Math.Max(strength, stability.Stability.GetValueOrDefault(cell + offset) - 1);

            AddSupport(cell, strength);
        }

        if (below is { } belowGrid && TryGetParticipatingGrid(map, out var receivingGrid) && receivingGrid == grid &&
            _stabilityQuery.TryGetComponent(belowGrid, out var belowStability) &&
            _gridQuery.TryGetComponent(belowGrid, out var belowGridComp))
        {
            foreach (var uid in belowStability.Supports)
            {
                if (!_supportQuery.TryGetComponent(uid, out var support) ||
                    !_xformQuery.TryGetComponent(uid, out var xform) || !xform.Anchored || xform.GridUid != belowGrid ||
                    !TryGetTileOnGrid(grid, gridComp, _transform.GetWorldPosition(xform), out var aboveCell) ||
                    !cells.Contains(aboveCell))
                    continue;

                var supportCell = _map.TileIndicesFor(belowGrid, belowGridComp, xform.Coordinates);
                AddSupport(aboveCell, Math.Min(support.SupportStrength,
                    belowStability.Stability.GetValueOrDefault(supportCell) - support.TransferLoss));
            }
        }

        var printable = new HashSet<Vector2i>(strengths.Keys);
        var pending = new Queue<Vector2i>(printable);
        while (pending.TryDequeue(out var cell))
        {
            var strength = strengths[cell] - 1;
            foreach (var offset in CEStabilityFloodFill.CardinalOffsets)
            {
                var next = cell + offset;
                if (cells.Contains(next) && AddSupport(next, strength))
                    pending.Enqueue(next);
            }
        }

        return (printable, new HashSet<Vector2i>(strengths.Keys));

        bool AddSupport(Vector2i cell, int strength)
        {
            if (strength <= strengths.GetValueOrDefault(cell))
                return false;

            strengths[cell] = strength;
            return true;
        }
    }

    private bool IsPending(EntityUid grid) =>
        _dirtyGrids.Contains(grid) || _busyGrids.Contains(grid) || _pendingIndexScan.Contains(grid);
}
