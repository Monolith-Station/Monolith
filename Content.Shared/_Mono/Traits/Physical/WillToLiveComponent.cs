namespace Content.Shared._Mono.Traits.Physical;

/// <summary>
/// Increases the damage threshold required to enter the Dead state.
/// </summary>
[RegisterComponent]
public sealed partial class WillToLiveComponent : Component
{
    /// <summary>
    /// How much to increase the Dead damage threshold by.
    /// </summary>
    [DataField]
    public int DeadIncrease = 10;
}
