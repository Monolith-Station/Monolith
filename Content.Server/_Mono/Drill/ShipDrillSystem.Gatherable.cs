using Content.Server.Gatherable.Components;

namespace Content.Server._Mono.Drill;

public sealed partial class ShipDrillSystem
{
    [Dependency] private EntityQuery<GatherableComponent> _gatherQuery = default!;

    public void DrillGatherable(EntityUid drilled, EntityUid drill)
    {
        if (_gatherQuery.TryComp(drilled, out var gather))
        {
            _gather.Gather(drilled, drill, gather, true);
        }
    }
}
