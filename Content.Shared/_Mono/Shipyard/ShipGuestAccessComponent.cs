using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Shipyard;

/// <summary>
/// Component that tracks which users have guest access to a ship.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipGuestAccessComponent : Component
{
    /// <summary>
    /// Set of ID card EntityUids that have been granted guest access to this ship.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> GuestIdCards = new();

    /// <summary>
    /// Set of cyborg EntityUids that have been granted guest access to this ship.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> GuestCyborgs = new();
}
