using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.ShipRepair;

[Serializable, NetSerializable]
public sealed partial class ShipRepairDoAfterEvent : SimpleDoAfterEvent
{
    public Vector2i TargetGridIndices;
    // if we're repairing an entity, store what we're repairing
    public bool IsEntityRepair = false;
    public int EntitySpecifierIndex;
}
