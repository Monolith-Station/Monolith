using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.Prototypes;

/// <summary>
/// This prototype is responsible for randomizing frequencies each round.
/// </summary>
[Prototype]
public sealed partial class PacketFrequencyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("minFrequency")]
    public int MinimalFrequency;

    [DataField("maxFrequency")]
    public int MaximumFrequency = 1;

    /// <summary>
    /// Current frequency
    /// </summary>
    [DataField]
    public int Frequency;

    /// <summary>
    /// Frequencies that this frequency allows by default.
    /// </summary>
    [DataField]
    public List<ProtoId<PacketFrequencyPrototype>> ListeningFrequencies = [];
}
