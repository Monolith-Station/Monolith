using Robust.Shared.GameStates;
using Robust.Shared.Network;

namespace Content.Shared._NF.Shipyard.Components;

/// <summary>
/// Tracks ownership of a ship grid and manages deletion when the owner has been offline too long
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipOwnershipComponent : Component
{
    /// <summary>
    /// The owner's player session ID
    /// </summary>
    [DataField, AutoNetworkedField]
    public NetUserId OwnerUserId;

    /// <summary>
    /// When the owner last connected or disconnected
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan LastStatusChangeTime;

    /// <summary>
    /// Whether the owner is currently online
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsOwnerOnline;

    /// <summary>
    /// How long to wait after the owner disconnects before deleting their ship (in seconds)
    /// </summary>
    [DataField]
    public float DeletionTimeoutSeconds = 3600; // 1 hour
}
