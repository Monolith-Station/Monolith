using System.Numerics;
using Content.Server._CE.ZLevels.Core;
using Content.Server.NodeContainer.Nodes;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.NodeContainer;
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

        if (xform.Comp.MapUid is not { } mapUid)
            yield break;

        var mapSystem = entMan.System<SharedMapSystem>();
        var gridIndex = mapSystem.TileIndicesFor(gridEnt, xform.Comp.Coordinates);

        List<Node> outputNodes = new();

        foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, gridEnt, gridIndex, mapSystem))
        {
            if (node is CableNode)
                outputNodes.Add(node);
        }

        var worldPos = entMan.System<SharedTransformSystem>().GetWorldPosition(xform.Owner);

        if (Up)
            CollectNeighbourNodes(mapUid, up: true, worldPos, nodeQuery, entMan, outputNodes);

        if (Down)
            CollectNeighbourNodes(mapUid, up: false, worldPos, nodeQuery, entMan, outputNodes);

        foreach (var node in outputNodes)
            yield return node;
    }

    private static void CollectNeighbourNodes(
        EntityUid mapUid,
        bool up,
        Vector2 worldPos,
        EntityQuery<NodeContainerComponent> nodeQuery,
        IEntityManager entMan,
        List<Node> outputNodes)
    {
        if (!TryGetNeighbourMap(mapUid, up, entMan, out var neighbourMap))
            return;

        var mapManager = IoCManager.Resolve<IMapManager>();
        var mapSystem = entMan.System<SharedMapSystem>();

        if (!mapManager.TryFindGridAt(neighbourMap, worldPos, out var gridUid, out var gridComp)
            || !mapSystem.TryGetTileRef(gridUid, gridComp, worldPos, out var tileRef)
            || tileRef.Tile.IsEmpty)
        {
            return;
        }

        foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, (gridUid, gridComp), tileRef.GridIndices, mapSystem))
        {
            if (node is CECableVerticalNode vertical && (up ? vertical.Down : vertical.Up))
                outputNodes.Add(node);
        }
    }
    private static bool TryGetNeighbourMap(EntityUid mapUid, bool up, IEntityManager entMan, out EntityUid neighbourMap)
    {
        neighbourMap = default;

        if (entMan.TryGetComponent<CEZTransitMapComponent>(mapUid, out var transit))
        {
            if ((up ? transit.TransitAbove : transit.TransitBelow) is not { } transitNeighbour)
                return false;

            neighbourMap = transitNeighbour;
            return true;
        }

        var zLevels = entMan.System<CEZLevelsSystem>();
        if (!(up ? zLevels.TryMapUp(mapUid, out var neighbour) : zLevels.TryMapDown(mapUid, out neighbour)))
            return false;

        neighbourMap = neighbour.Owner;
        return true;
    }
}
