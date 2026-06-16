using Content.Server.Gatherable;
using JetBrains.Annotations;

namespace Content.Server._Mono.Drill.Types;

[UsedImplicitly]
public sealed partial class GatheringDrillType : DrillType
{
    public override void Drill(EntityUid drilled, EntityUid drill, ShipDrillSystem shipDrill, EntityManager manager)
    {
        shipDrill.DrillGatherable(drilled, drill);
    }
}
