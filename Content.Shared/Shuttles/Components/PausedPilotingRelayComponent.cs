using Robust.Shared.GameStates;

namespace Content.Shared.Shuttles.Components;

/// <summary>
/// Temporary component used to store the target of a RelayInputMoverComponent
/// when it's removed because the entity started piloting.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PausedPilotingRelayComponent : Component
{
    [DataField]
    public EntityUid RelayTarget;
}