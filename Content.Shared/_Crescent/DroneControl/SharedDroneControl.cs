using Content.Shared.Crescent.Radar;
using Content.Shared.Shuttles.BUIStates;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared._Crescent.DroneControl;

[Serializable, NetSerializable]
public enum DroneConsoleUiKey : byte
{
    Key
}

/// <summary>
///     Added to the Console entity.
///     Requires DeviceNetworkComponent and DeviceListComponent to function.
/// </summary>
[RegisterComponent]
public sealed partial class DroneControlConsoleComponent : Component
{
}

/// <summary>
///     Added to Drones to allow them to receive orders.
///     Requires DeviceNetworkComponent and HTNComponent.
/// </summary>
[RegisterComponent]
public sealed partial class DroneControlComponent : Component
{
}

[Serializable, NetSerializable]
public sealed class DroneConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public NavInterfaceState NavState;
    public IFFInterfaceState IFFState;

    // Key: NetEntity of the drone, Value: Name
    public List<NetEntity> LinkedDrones;

    public DroneConsoleBoundUserInterfaceState(
        NavInterfaceState navState,
        IFFInterfaceState iffState,
        List<NetEntity> linkedDrones)
    {
        NavState = navState;
        IFFState = iffState;
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
    public Vector2 TargetCoordinates;

    public DroneConsoleMoveMessage(HashSet<NetEntity> selectedDrones, Vector2 targetCoordinates)
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
    public NetEntity TargetGrid;

    public DroneConsoleTargetMessage(HashSet<NetEntity> selectedDrones, NetEntity targetGrid)
    {
        SelectedDrones = selectedDrones;
        TargetGrid = targetGrid;
    }
}

/// <summary>
///     Constants for DeviceNetwork packet keys.
/// </summary>
public static class DroneConsoleConstants
{
    public const string CommandMove = "drone_cmd_move";
    public const string CommandTarget = "drone_cmd_target";
    public const string KeyCoords = "coords";
    public const string KeyEntity = "entity";
}
