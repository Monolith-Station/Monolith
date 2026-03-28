using Content.Server.NodeContainer;
using Content.Server.Power.Nodes;
using Robust.Shared.Map.Components;

namespace Content.Server.NodeContainer.Nodes;

/// <summary>
/// Internal cable node for gaslock power bridging.
/// Connects to local cables and to the configured dock node on the same entity.
/// </summary>
[DataDefinition]
public sealed partial class GaslockPowerBridgeNode : CableDeviceNode
{
    [DataField(required: true)]
    public string DockNode = string.Empty;

    public override IEnumerable<Node> GetReachableNodes(
        TransformComponent xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        MapGridComponent? grid,
        IEntityManager entMan)
    {
        if (!xform.Anchored || grid == null)
            yield break;

        var yielded = new HashSet<Node>();

        foreach (var node in base.GetReachableNodes(xform, nodeQuery, xformQuery, grid, entMan))
        {
            if (yielded.Add(node))
                yield return node;
        }

        // Gaslocks are full-tile structures, so adjacent cable connectivity is required
        // in maps where under-tile cabling is not used on the same tile.
        var gridIndex = grid.TileIndicesFor(xform.Coordinates);
        foreach (var (_, node) in NodeHelpers.GetCardinalNeighborNodes(nodeQuery, grid, gridIndex))
        {
            if (node is CableNode && yielded.Add(node))
                yield return node;
        }

        if (!nodeQuery.TryComp(Owner, out var nodeContainer) ||
            string.IsNullOrEmpty(DockNode) ||
            !nodeContainer.Nodes.TryGetValue(DockNode, out var dockNode))
        {
            yield break;
        }

        if (dockNode is CableDeviceNode &&
            dockNode.NodeGroupID == NodeGroupID)
        {
            if (yielded.Add(dockNode))
                yield return dockNode;
        }
    }
}
