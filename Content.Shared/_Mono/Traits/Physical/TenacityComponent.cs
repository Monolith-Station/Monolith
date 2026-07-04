namespace Content.Shared._Mono.Traits.Physical;

/// <summary>
/// Increases the damage threshold required to enter the Critical state.
/// </summary>
[RegisterComponent]
public sealed partial class TenacityComponent : Component
{
    /// <summary>
    /// How much to increase the Critical damage threshold by.
    /// </summary>
    [DataField]
    public int CritIncrease = 5;
}


