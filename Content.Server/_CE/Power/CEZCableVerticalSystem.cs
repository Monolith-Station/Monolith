/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server.NodeContainer.EntitySystems;
using Content.Server.Power.Nodes;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.NodeContainer;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.Power;

/// <summary>
/// Keeps <see cref="CECableVerticalNode"/> connections in sync with the z-grid network.
/// </summary>
public sealed partial class CEZCableVerticalSystem : EntitySystem
{
    [Dependency] private NodeGroupSystem _nodeGroup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MapGridComponent, CEGridAddedIntoZNetworkEvent>(OnGridLinked);
        SubscribeLocalEvent<MapGridComponent, CEGridRemovedFromZNetworkEvent>(OnGridUnlinked);
        SubscribeLocalEvent<MapGridComponent, CEZLevelMapMoveEvent>(OnGridZMoved);
    }

    private void OnGridZMoved(Entity<MapGridComponent> grid, ref CEZLevelMapMoveEvent args)
    {
        RefloodVerticalNodes(grid.Owner);
    }

    private void OnGridLinked(Entity<MapGridComponent> grid, ref CEGridAddedIntoZNetworkEvent args)
    {
        RefloodVerticalNodes(grid.Owner);
    }

    private void OnGridUnlinked(Entity<MapGridComponent> grid, ref CEGridRemovedFromZNetworkEvent args)
    {
        RefloodVerticalNodes(grid.Owner);
    }

    private void RefloodVerticalNodes(EntityUid gridUid)
    {
        var enumerator = EntityQueryEnumerator<NodeContainerComponent, TransformComponent>();
        while (enumerator.MoveNext(out _, out var nc, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            foreach (var node in nc.Nodes.Values)
            {
                if (node is CECableVerticalNode)
                    _nodeGroup.QueueReflood(node);
            }
        }
    }
}
