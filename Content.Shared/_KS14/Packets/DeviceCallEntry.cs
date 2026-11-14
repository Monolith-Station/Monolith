using Robust.Shared.Serialization;

namespace Content.Shared._KS14.Packets;

[Serializable, NetSerializable]
public sealed class DeviceCallEntry
{
    public string DeviceCallName;

    public Dictionary<string, string> InVariables;
    public Dictionary<string, string> OutVariables;

    public DeviceCallEntry(string deviceCallName, Dictionary<string, string> inVariables, Dictionary<string, string> outVariables)
    {
        DeviceCallName = deviceCallName;
        InVariables = inVariables;
        OutVariables = outVariables;
    }
}
