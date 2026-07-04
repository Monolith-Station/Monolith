namespace Content.Shared._Mono.Traits.Physical;

/// <summary>
/// Decreases the damage threshold required to enter the Dead state.
/// </summary>
[RegisterComponent]
public sealed partial class WillToDieComponent : Component
{
    /// <summary>
    /// How much to decrease the Dead damage threshold by.
    /// </summary>
    [DataField]
    public int DeadDecrease = 15;
}


