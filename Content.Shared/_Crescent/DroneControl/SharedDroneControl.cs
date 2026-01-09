using Content.Shared.Shuttles.BUIStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.DroneControl;

[Serializable, NetSerializable]
public enum DroneConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class DroneConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public NavInterfaceState NavState;

    // Key: NetEntity of the drone, Value: Name
    public List<(NetEntity Server, NetEntity Grid)> LinkedDrones;

    public DroneConsoleBoundUserInterfaceState(
        NavInterfaceState navState,
        List<(NetEntity, NetEntity)> linkedDrones)
    {
        NavState = navState;
        LinkedDrones = linkedDrones;
    }
}

/// <summary>
///     Sent when the client determines the click was in empty space.
/// </summary>
[Serializable, NetSerializable]
public sealed class DroneConsoleMoveMessage : BoundUserInterfaceMessage
{
    public HashSet<NetEntity> SelectedDrones;
    public NetCoordinates TargetCoordinates;

    public DroneConsoleMoveMessage(HashSet<NetEntity> selectedDrones, NetCoordinates targetCoordinates)
    {
        SelectedDrones = selectedDrones;
        TargetCoordinates = targetCoordinates;
    }
}

/// <summary>
///     Sent when the client determines the click hit a grid.
/// </summary>
[Serializable, NetSerializable]
public sealed class DroneConsoleTargetMessage : BoundUserInterfaceMessage
{
    public HashSet<NetEntity> SelectedDrones;
    public NetCoordinates TargetCoordinates;

    public DroneConsoleTargetMessage(HashSet<NetEntity> selectedDrones, NetCoordinates targetCoordinates)
    {
        SelectedDrones = selectedDrones;
        TargetCoordinates = targetCoordinates;
    }
}

/// <summary>
///     Constants for DeviceNetwork packet keys.
/// </summary>
public static class DroneConsoleConstants
{
    public const string CommandMove = "drone_cmd_move";
    public const string CommandTarget = "drone_cmd_target";
    public const string TargetCoords = "target";
}

public enum DroneOrderType
{
    Move,
    Target
}
