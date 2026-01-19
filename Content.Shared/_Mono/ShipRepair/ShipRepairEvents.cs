using Content.Shared._Mono.ShipRepair.Components;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.ShipRepair;

/// <summary>
/// Raised to check if entity should be included in repair data.
/// </summary>
[ByRefEvent]
public record struct ShipRepairStoreQueryEvent(bool Repairable = true);

/// <summary>
/// Raised to check on the original of an entity we're trying to reinstate, if such an original still exists.
/// </summary>
[ByRefEvent]
public record struct ShipRepairReinstateQueryEvent(bool Repairable = true, bool Handled = false);

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

[Serializable, NetSerializable]
public sealed partial class RepairEntityMessage : EntityEventArgs
{
    public NetEntity Grid;
    public Vector2i Indices;
    public int SpecId;
    public ShipRepairEntitySpecifier NewSpec;

    public RepairEntityMessage(NetEntity grid, Vector2i indices, int specId, ShipRepairEntitySpecifier newSpec)
    {
        Grid = grid;
        Indices = indices;
        SpecId = specId;
        NewSpec = newSpec;
    }
}
