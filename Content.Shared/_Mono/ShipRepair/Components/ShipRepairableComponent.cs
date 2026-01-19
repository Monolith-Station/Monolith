using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.ShipRepair.Components;

/// <summary>
/// Entity that is repairable via <see cref="ShipRepairToolComponent.cs"/>
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipRepairableComponent : Component
{
    /// <summary>
    /// If not null, what entity should be placed when this is repaired.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? RepairTo = null;

    [DataField, AutoNetworkedField]
    public float RepairTime = 2f;

    /// <summary>
    /// How many charges to use from <see cref="LimitedChargesComponent"/>.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public int RepairCost = 2;
}
