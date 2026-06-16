using Content.Server.Gatherable.Components;

namespace Content.Server._Mono.Drill;

public sealed partial class ShipDrillSystem
{
    private EntityQuery<GatherableComponent> _gatherQuery;

    private void InitializeGatherDrill()
    {
        _gatherQuery = GetEntityQuery<GatherableComponent>();
    }

    public void DrillGatherable(EntityUid uid)
    {
        if (_gatherQuery.TryComp(uid, out var gather))
        {
            _gather.Gather(uid, null, gather);
        }
    }
}
