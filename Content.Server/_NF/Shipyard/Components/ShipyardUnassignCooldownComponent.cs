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
    /// #Mono Lowered to 25 minutes, so FUBAR LPC ships can be renewed more often.
    /// </summary>
    [DataField]
    public TimeSpan CooldownDuration = TimeSpan.FromMinutes(25);

    /// <summary>
    /// When the player can next unassign a deed.
    /// </summary>
    [DataField]
    public TimeSpan NextUnassignTime;
}
