using Robust.Shared.GameStates;

namespace Content.Shared._KS14.Packets;

/// <summary>
/// This is used for packet network configurators - Handheld tool made to interact with executors/packet networks
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
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
    public List<int> Frequencies = [];

    [DataField]
    [AutoNetworkedField]
    public int ActiveFrequency = -1;

    [DataField]
    [AutoNetworkedField]
    public string FrequencyName = "None";

    /// <summary>
    /// Current configurator mode.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public ConfiguratorMode Mode = ConfiguratorMode.Probe;
}

public enum ConfiguratorMode
{
    Probe,
    Save
}
