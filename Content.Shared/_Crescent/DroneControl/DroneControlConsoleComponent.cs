namespace Content.Shared._Crescent.DroneControl;

/// <summary>
///     Allows an entity to send drone control orders to linked drone control servers.
/// </summary>
[RegisterComponent]
public sealed partial class DroneControlConsoleComponent : Component
{
    /// <summary>
    /// Maximum distance orders can tell drones to go to, if not null.
    /// </summary>
    [DataField]
    public float? MaxOrderRadius = null;
}
