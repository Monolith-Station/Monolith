using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Ships;

[Serializable, NetSerializable]
public enum ShipShieldProjectorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ShipShieldProjectorToggleMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ShipShieldProjectorUiState(
    bool locked, float receivedPower, float requestedPower) : BoundUserInterfaceState
{
    public readonly bool Locked = locked;
    public readonly float ReceivedPower = receivedPower;
    public readonly float RequestedPower = requestedPower;
}
