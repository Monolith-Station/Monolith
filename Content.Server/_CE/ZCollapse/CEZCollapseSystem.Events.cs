/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

namespace Content.Server._CE.ZCollapse;

public sealed partial class CEZCollapseSystem
{
    private void OnCoreAnchorChanged(Entity<CEGridStabilityCoreComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Transform.GridUid is not { } gridUid || !_stabilityQuery.TryGetComponent(gridUid, out var comp))
            return;

        if (args.Anchored)
            comp.Cores.Add(ent.Owner);
        else
            comp.Cores.Remove(ent.Owner);
        MarkDirty(gridUid);
    }

    private void OnCoreReAnchor(Entity<CEGridStabilityCoreComponent> ent, ref ReAnchorEvent args)
    {
        if (_stabilityQuery.TryGetComponent(args.OldGrid, out var oldComp))
        {
            oldComp.Cores.Remove(ent.Owner);
            MarkDirty(args.OldGrid);
        }

        if (_stabilityQuery.TryGetComponent(args.Grid, out var newComp))
        {
            newComp.Cores.Add(ent.Owner);
            MarkDirty(args.Grid);
        }
    }

    private void OnSupportAnchorChanged(Entity<CEGridStabilitySupportComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Transform.GridUid is not { } gridUid || !_stabilityQuery.TryGetComponent(gridUid, out var comp))
            return;

        if (args.Anchored)
            comp.Supports.Add(ent.Owner);
        else
            comp.Supports.Remove(ent.Owner);
        MarkDirty(gridUid);
    }

    private void OnSupportReAnchor(Entity<CEGridStabilitySupportComponent> ent, ref ReAnchorEvent args)
    {
        if (_stabilityQuery.TryGetComponent(args.OldGrid, out var oldComp))
        {
            oldComp.Supports.Remove(ent.Owner);
            MarkDirty(args.OldGrid);
        }

        if (_stabilityQuery.TryGetComponent(args.Grid, out var newComp))
        {
            newComp.Supports.Add(ent.Owner);
            MarkDirty(args.Grid);
        }
    }

    private void OnTileChanged(Entity<CEGridStabilityComponent> ent, ref TileChangedEvent args)
    {
        MarkDirty(ent.Owner);
    }

    private void OnStabilityMapInit(Entity<CEGridStabilityComponent> ent, ref MapInitEvent args)
    {
        _pendingIndexScan.Add(ent.Owner);
    }

    private void OnGridSplit(Entity<CEGridStabilityComponent> ent, ref GridSplitEvent args)
    {
        _pendingIndexScan.Add(ent.Owner);

        foreach (var newGrid in args.NewGrids)
        {
            EnsureComp<CEGridStabilityComponent>(newGrid);
            _pendingIndexScan.Add(newGrid);
        }
    }
}
