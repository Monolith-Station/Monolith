using Content.Server.Construction.Components;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._NF.Power.Components;

[RegisterComponent]
public sealed partial class UpgradePowerSupplyRampingComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public float BaseRampRate;

    /// <summary>
    ///     The machine part that affects the power supply ramping
    /// </summary>
    [DataField("machinePartPowerCapacity")]
    public ProtoId<MachinePartPrototype> MachinePartRampRate = "Capacitor";

    /// <summary>
    ///     The multiplier used for scaling the power supply ramping
    /// </summary>
    [DataField("supplyRampingMultiplier")]
    public float SupplyRampingMultiplier = 1f;

    /// <summary>
    ///     What type of scaling is being used?
    /// </summary>
    [DataField("scaling", required: true), ViewVariables(VVAccess.ReadWrite)]
    public MachineUpgradeScalingType Scaling;

    /// <summary>
    ///     The current value that the power supply is being scaled by
    /// </summary>
    [DataField("actualScalar"), ViewVariables(VVAccess.ReadWrite)]
    public float ActualScalar = 1f;
}
