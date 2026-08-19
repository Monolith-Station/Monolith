using Robust.Shared.GameStates;

namespace Content.Shared._Forge.ShipyardService.Components;

/// <summary>
/// Drydock airlock. Shuttles docked here can be serviced by a shipyard console.
/// Occupancy fee is charged only when the shuttle is repaired, not for merely docking.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipyardDockComponent : Component
{
    /// <summary>
    /// Fraction of the docked shuttle's listed price charged when using repair.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FeePercent = 0.1f;

    /// <summary>
    /// True after the occupancy fee was collected for this docking session.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool OccupancyCharged;

    [DataField, AutoNetworkedField]
    public int CachedFee;

    [DataField, AutoNetworkedField]
    public string CachedShuttleName = string.Empty;
}
