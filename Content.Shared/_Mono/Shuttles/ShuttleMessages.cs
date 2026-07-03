using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Shuttles;

/// <summary>
/// Raised on the client when it wishes to travel somewhere via autopilot.
/// </summary>
[Serializable, NetSerializable]
public sealed class ShuttleConsoleAutopilotPositionMessage : BoundUserInterfaceMessage
{
    public MapCoordinates Coordinates;
    public Angle Angle;
}
