using Content.Server._CE.ZLevels.Core;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.NodeContainer;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Power.Nodes;

[DataDefinition]
public sealed partial class CECableVerticalNode : Node
{
    [DataField]
    public bool Up;

    [DataField]
    public bool Down;

    public override IEnumerable<Node> GetReachableNodes(
        Entity<TransformComponent> xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        Entity<MapGridComponent>? grid,
        IEntityManager entMan)
    {
        if (!xform.Comp.Anchored || grid is not { } gridEnt)
            yield break;

        if (xform.Comp.MapUid is null)
            yield break;

        var mapSystem = entMan.System<SharedMapSystem>();
        var mapManager = IoCManager.Resolve<IMapManager>();
        var zLevelsSys = entMan.System<CEZLevelsSystem>();
        var worldPos = entMan.System<SharedTransformSystem>().GetWorldPosition(xform.Owner);

        var gridIndex = mapSystem.TileIndicesFor(gridEnt, xform.Comp.Coordinates);

        List<Node> outputNodes = new();

        foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, gridEnt, gridIndex, mapSystem))
        {
            if (node is CableNode)
                outputNodes.Add(node);
        }

        if (Up && zLevelsSys.TryMapUp(xform.Comp.MapUid.Value, out var mapAbove))
        {
            if (mapManager.TryFindGridAt(mapAbove.Owner, worldPos, out var gridAboveUid, out var gridAboveComp)
                && mapSystem.TryGetTileRef(gridAboveUid, gridAboveComp, worldPos, out var tileAbove)
                && !tileAbove.Tile.IsEmpty)
            {
                foreach (var nodeAbove in NodeHelpers.GetNodesInTile(nodeQuery, (gridAboveUid, gridAboveComp), tileAbove.GridIndices, mapSystem))
                {
                    if (nodeAbove is CECableVerticalNode verticalCableNode && verticalCableNode.Down)
                        outputNodes.Add(nodeAbove);
                }
            }
        }

        if (Down && zLevelsSys.TryMapDown(xform.Comp.MapUid.Value, out var mapBelow))
        {
            if (mapManager.TryFindGridAt(mapBelow.Owner, worldPos, out var gridBelowUid, out var gridBelowComp)
                && mapSystem.TryGetTileRef(gridBelowUid, gridBelowComp, worldPos, out var tileBelow)
                && !tileBelow.Tile.IsEmpty)
            {
                foreach (var nodeBelow in NodeHelpers.GetNodesInTile(nodeQuery, (gridBelowUid, gridBelowComp), tileBelow.GridIndices, mapSystem))
                {
                    if (nodeBelow is CECableVerticalNode verticalCableNode && verticalCableNode.Up)
                        outputNodes.Add(nodeBelow);
                }
            }
        }

        foreach (var node in outputNodes)
            yield return node;
    }
}
