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
    /// Last time any non-ghost player was detected on this ship.
    /// </summary>
    [DataField]
    public TimeSpan LastPlayerActivityTime;

    /// <summary>
    /// When set, the ship is scheduled to be auto-deleted at this time unless activity resumes.
    /// </summary>
    [DataField]
    public TimeSpan? PendingDeletionTime;
}
