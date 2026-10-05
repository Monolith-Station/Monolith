using System.Linq;
using Content.Shared._Mono.MassFabricator;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Mono.MassFabricator;

public sealed partial class MassFabricatorHologramSystem
{
    private sealed class PrintNode
    {
        public bool Confirmed;
        public bool Expanded;
        public bool AwaitDoAfter;
        public bool Ready;
        public EntityUid? PredictedGrid;
        public Vector2i TileIndex;
        public Tile OriginalTile;
        public Tile PrintedTile;
    }

    private readonly Dictionary<EntityUid, PrintNode> _printWaves = new();

    private float VisualPrintDuration(MassFabricatorHologramComponent plan)
    {
        if (plan.PrintDuration > 0f)
            return plan.PrintDuration;

        var maxLoad = TryComp<MassFabricatorComponent>(plan.Fabricator, out var gun)
            ? gun.MaxBlueprintLoad : 100;
        return _prototypes.TryIndex(plan.Blueprint, out var blueprint)
            ? blueprint.PrintDuration(maxLoad) : 0.6f;
    }

    public void TrackManualPrint(Entity<MassFabricatorHologramComponent> ent)
    {
        if (_printWaves.ContainsKey(ent.Owner))
            return;

        _printVisualStarts[ent.Owner] = _timing.CurTime;
        _printWaves.Add(ent.Owner, new PrintNode { AwaitDoAfter = true });
    }

    public void TrackAutomaticPrint(Entity<MassFabricatorHologramComponent> parent,
        Entity<MassFabricatorHologramComponent> neighbor)
    {
        if (_printWaves.ContainsKey(neighbor.Owner))
            return;

        _printVisualStarts[neighbor.Owner] = _printVisualStarts.TryGetValue(parent.Owner, out var start)
            ? start + TimeSpan.FromSeconds(VisualPrintDuration(parent.Comp)) : _timing.CurTime;
        _printWaves.Add(neighbor.Owner, new PrintNode());
    }

    public void OnManualPrintResult(Entity<MassFabricatorHologramComponent> ent, bool cancelled)
    {
        if (!_printWaves.TryGetValue(ent.Owner, out var node) || !node.AwaitDoAfter)
            return;

        if (cancelled)
        {
            RollbackPrediction(node, false);
            _printWaves.Remove(ent.Owner);
            _printVisualStarts.Remove(ent.Owner);
        }
        else
        {
            node.Ready = true;
        }
    }

    private void ConfirmPrint(Entity<MassFabricatorHologramComponent> ent)
    {
        if (!_prototypes.TryIndex(ent.Comp.Blueprint, out _))
            return;

        if (!_printWaves.TryGetValue(ent.Owner, out var node))
        {
            node = new PrintNode();
            _printWaves.Add(ent.Owner, node);
        }

        node.Confirmed = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var transforms = EntityManager.System<SharedTransformSystem>();
        var maps = EntityManager.System<SharedMapSystem>();
        foreach (var (uid, node) in _printWaves.ToArray())
        {
            if (!_printVisualStarts.TryGetValue(uid, out var started) ||
                !TryComp<MassFabricatorHologramComponent>(uid, out var plan))
                continue;

            if (node.Expanded)
            {
                if (plan.PrintState != MassFabricatorPrintState.Fading &&
                    node.PredictedGrid is { } predictedGrid &&
                    TryComp<MapGridComponent>(predictedGrid, out var predictedMapGrid) &&
                    maps.GetTileRef(predictedGrid, predictedMapGrid, node.TileIndex).Tile == node.OriginalTile)
                    maps.SetTile(predictedGrid, predictedMapGrid, node.TileIndex, node.PrintedTile);
                continue;
            }

            var end = started + TimeSpan.FromSeconds(VisualPrintDuration(plan));
            if (_timing.CurTime < end || node.AwaitDoAfter && !node.Ready &&
                plan.PrintState != MassFabricatorPrintState.Fading)
                continue;

            node.Expanded = true;
            if (!node.Confirmed && !node.Ready)
            {
                _printWaves.Remove(uid);
                _printVisualStarts.Remove(uid);
                continue;
            }

            PredictCompletion(uid, plan, node, transforms, maps);
        }
    }

    private void PredictCompletion(EntityUid uid, MassFabricatorHologramComponent plan, PrintNode node,
        SharedTransformSystem transforms, SharedMapSystem maps)
    {
        if (plan.PrintState == MassFabricatorPrintState.Fading)
            return;

        var coords = Transform(uid).Coordinates;
        if (plan.Kind == MassFabricatorBlueprintKind.Tile &&
            _prototypes.TryIndex<ContentTileDefinition>(plan.Target, out var tile) &&
            transforms.GetGrid(coords) is { } grid && TryComp<MapGridComponent>(grid, out var mapGrid))
        {
            var index = maps.CoordinatesToTile(grid, mapGrid, coords);
            node.PredictedGrid = grid;
            node.TileIndex = index;
            node.OriginalTile = maps.GetTileRef(grid, mapGrid, index).Tile;
            node.PrintedTile = new Tile(tile.TileId);
            maps.SetTile(grid, mapGrid, index, node.PrintedTile);
        }
    }

    private void RollbackPrediction(PrintNode node, bool completed)
    {
        if (completed || node.PredictedGrid is not { } grid ||
            !TryComp<MapGridComponent>(grid, out var mapGrid))
            return;

        var maps = EntityManager.System<SharedMapSystem>();
        if (maps.GetTileRef(grid, mapGrid, node.TileIndex).Tile == node.PrintedTile)
            maps.SetTile(grid, mapGrid, node.TileIndex, node.OriginalTile);
    }
}
