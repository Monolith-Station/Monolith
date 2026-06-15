using JetBrains.Annotations;

namespace Content.Shared._Mono.Drill.Types;

[UsedImplicitly]
public sealed partial class GatheringDrillType : DrillType
{
    public bool TeleportGatheredToDrill = false;

    public override void Drill(EntityUid gridUid, EntityManager system, IComponentFactory? factory = null)
    {

    }
}
