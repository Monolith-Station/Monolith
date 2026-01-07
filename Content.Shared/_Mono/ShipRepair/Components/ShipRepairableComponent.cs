using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.ShipRepair.Components;

/// <summary>
/// Entity that is repairable via <see cref="ShipRepairToolComponent.cs"/>
/// </summary>
[RegisterComponent]
public sealed partial class ShipRepairableComponent : Component
{
    /// <summary>
    /// If not null, what entity should be placed when this is repaired.
    /// </summary>
    [DataField]
    public EntProtoId? RepairTo = null;

    [DataField]
    public float RepairTime = 2f;

    /// <summary>
    /// How many charges to use from <see cref="LimitedChargesComponent"/>.
    /// </summary>
    [DataField(required: true)]
    public int RepairCost = 2;
}
