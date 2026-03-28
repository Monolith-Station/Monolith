using Content.Server.Shuttles.Components;
using Content.Server.Power.Nodes;
using Robust.Shared.Map.Components;

namespace Content.Server.NodeContainer.Nodes;

/// <summary>
/// Cable node that only connects to matching dock nodes on the docked port.
/// It intentionally does not connect to local tile cables.
/// </summary>
[DataDefinition]
public sealed partial class DockableCableNode : CableDeviceNode
{
    public override IEnumerable<Node> GetReachableNodes(
        TransformComponent xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        MapGridComponent? grid,
        IEntityManager entMan)
    {
        if (!xform.Anchored || grid == null)
            yield break;

        if (!entMan.TryGetComponent(Owner, out DockingComponent? docking) ||
            docking.DockedWith == null ||
            !nodeQuery.TryComp(docking.DockedWith.Value, out var otherNodeContainer))
        {
            yield break;
        }

        foreach (var node in otherNodeContainer.Nodes.Values)
        {
            if (node is DockableCableNode dockable &&
                dockable.NodeGroupID == NodeGroupID)
            {
                yield return dockable;
            }
        }
    }
}
