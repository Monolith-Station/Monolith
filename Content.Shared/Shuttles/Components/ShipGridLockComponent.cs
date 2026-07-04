using Content.Shared.Shuttles.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Shuttles.Components;

/// <summary>
/// Component that handles grid-level locking for ships with deeds.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedShuttleConsoleLockSystem))]
public sealed partial class ShipGridLockComponent : Component
{
    /// <summary>
    /// Whether the ship grid is currently locked.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Locked = true;

    /// <summary>
    /// The ID of the shuttle this grid is locked to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? ShuttleId;
}
