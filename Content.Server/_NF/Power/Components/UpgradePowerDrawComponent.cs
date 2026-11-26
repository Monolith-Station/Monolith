using Content.Server.Construction.Components;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._NF.Power.Components;

/// <summary>
/// This is used for machines whose power draw
/// can be decreased through machine part upgrades.
/// </summary>
[RegisterComponent]
public sealed partial class UpgradePowerDrawComponent : Component
{
    /// <summary>
    /// The base power draw of the machine.
    /// Prioritizes hv/mv draw over lv draw.
    /// Value is initializezd on map init from <see cref="ApcPowerReceiverComponent"/>
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float BaseLoad;

    /// <summary>
    /// The machine part that affects the power draw.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<MachinePartPrototype> MachinePartPowerDraw = "Capacitor";

    /// <summary>
    /// The multiplier used for scaling the power draw.
    /// </summary>
    [DataField("powerDrawMultiplier", required: true), ViewVariables(VVAccess.ReadWrite)]
    public float PowerDrawMultiplier = 1f;

    /// <summary>
    /// What type of scaling is being used?
    /// </summary>
    [DataField("scaling", required: true), ViewVariables(VVAccess.ReadWrite)]
    public MachineUpgradeScalingType Scaling;
}
