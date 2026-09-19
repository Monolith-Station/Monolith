namespace Content.Server._KS14.Packet.Components;

/// <summary>
/// This is used for packet network configurators - Handheld tool made to interact with executors/packet networks
/// </summary>
[RegisterComponent]
public sealed partial class PacketNetworkConfiguratorComponent : Component
{
    /// <summary>
    /// List mode: Addresses it currently stores.
    /// Will create network upon using.
    /// </summary>
    [DataField]
    public List<String> Addresses = new();

    /// <summary>
    /// Probe & List mode: Current frequency it listens to.
    /// If frequency doesn't match with packet network device - denies it.
    /// </summary>
    [DataField]
    public int Frequency;

    /// <summary>
    /// Current configurator mode.
    /// </summary>
    [DataField]
    public ConfiguratorMode Mode = ConfiguratorMode.Probe;
}

public enum ConfiguratorMode
{
    Probe,
    Save
}
