using Robust.Shared.Serialization;

namespace Content.Server._NF.Shipyard.Components;

/// <summary>
/// Component that tracks when a player last unassigned a ship deed.
/// This is used to implement a cooldown on the unassign feature.
/// </summary>
[RegisterComponent]
public sealed partial class ShipyardUnassignCooldownComponent : Component
{
    /// <summary>
    /// How long the player must wait between unassign actions (1 hour).
    /// </summary>
    [DataField]
    public TimeSpan CooldownDuration = TimeSpan.FromHours(1);

    /// <summary>
    /// When the player can next unassign a deed.
    /// </summary>
    [DataField]
    public TimeSpan NextUnassignTime;
}
