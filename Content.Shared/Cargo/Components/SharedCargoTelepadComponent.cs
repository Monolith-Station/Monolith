using Content.Shared.Construction.Prototypes;
using Content.Shared.DeviceLinking;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Cargo.Components;

/// <summary>
/// Handles teleporting in requested cargo after the specified delay.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedCargoSystem))]
public sealed partial class CargoTelepadComponent : Component
{
    /// <summary>
    /// The base amount of time it takes to teleport from the telepad
    /// </summary>
    [DataField("baseDelay"), ViewVariables(VVAccess.ReadWrite)]
    public float BaseDelay = 10f;

    /// <summary>
    /// The actual amount of time it takes to teleport from the telepad
    /// </summary>
    [DataField("delay"), ViewVariables(VVAccess.ReadWrite)]
    public float Delay = 10f;

    /// <summary>
    /// The machine part that affects <see cref="Delay"/>
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<MachinePartPrototype> MachinePartTeleportDelay = "Capacitor";

    /// <summary>
    /// A multiplier applied to <see cref="Delay"/> for each level of <see cref="MachinePartTeleportDelay"/>
    /// </summary>
    [DataField("partRatingTeleportDelay"), ViewVariables(VVAccess.ReadWrite)]
    public float PartRatingTeleportDelay = 0.8f;

    /// <summary>
    /// How much time we've accumulated until next teleport.
    /// </summary>
    [DataField("accumulator"), ViewVariables(VVAccess.ReadWrite)]
    public float Accumulator;

    [DataField("currentState")]
    public CargoTelepadState CurrentState = CargoTelepadState.Unpowered;

    [DataField("teleportSound")]
    public SoundSpecifier TeleportSound = new SoundPathSpecifier("/Audio/Machines/phasein.ogg");

    /// <summary>
    ///     The paper-type prototype to spawn with the order information.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId PrinterOutput = "PaperCargoInvoice";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<SinkPortPrototype> ReceiverPort = "OrderReceiver";
}
