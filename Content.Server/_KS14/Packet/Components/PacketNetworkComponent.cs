using Content.Server._KS14.Packet.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.Components;

/// <summary>
/// Entities with this component are able to receive packets.
/// Packet will be delivered only if both address and frequency match with the sender's packet.
/// </summary>
[RegisterComponent]
public sealed partial class PacketNetworkComponent : Component
{
    /// <summary>
    /// Frequency is randomized each round based on prototype.
    /// The more dangerous device is - the longer it would take to bruteforce it.
    /// Act as a password, address is used as true identification.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<PacketFrequencyPrototype> Frequency;

    /// <summary>
    /// Frequencies that packet network currently accepts.
    /// This includes:
    /// A: Allowed frequencies from packet frequency prototype.
    /// B: Allowed frequencies from executor command.
    /// </summary>
    [DataField]
    public List<ProtoId<PacketFrequencyPrototype>> ListeningFrequencies = [];

    /// <summary>
    /// Does this packet network accepts signals from other grids?
    /// </summary>
    [DataField]
    public bool IsGlobal;

    /// <summary>
    /// Each device has different address, even if frequency is not the same.
    /// Assigned randomly upon entity initialization
    /// </summary>
    [DataField]
    public string Address = "0x000000";

    /// <summary>
    /// Network that is assigned to this device <see cref="PacketNetwork"/>
    /// </summary>
    [DataField]
    public string? AddressNetwork;
}
