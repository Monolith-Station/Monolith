using Content.Shared._Mono.Sensors;
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

/// <summary>
/// Wrapper around <see cref="NavInterfaceState"/>
/// </summary>
[Serializable, NetSerializable]
public sealed class NavBoundUserInterfaceState : BoundUserInterfaceState
{
    public NavInterfaceState State;
    public SensorsBuiState? Sensors; // Mono

    public NavBoundUserInterfaceState(NavInterfaceState state, SensorsBuiState? sensors = null) // Mono
    {
        State = state;
        Sensors = sensors; // Mono
    }
}
