using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.ShipRepair;

[Serializable, NetSerializable]
public sealed partial class ShipRepairDoAfterEvent : SimpleDoAfterEvent
{
    public Vector2i TargetGridIndices;
    public int Cost;
    // if we're repairing an entity, store what we're repairing
    public int? RepairId = null;

    public override bool IsDuplicate(DoAfterEvent other)
    {
        if (other is not ShipRepairDoAfterEvent cast)
            return false;

        return TargetGridIndices == cast.TargetGridIndices && RepairId == cast.RepairId;
    }
}
