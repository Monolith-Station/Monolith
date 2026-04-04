namespace Content.Shared._Mono.Traits.Physical;

/// <summary>
/// Offsets the threshold required to reach mob thresholds.
/// </summary>
[RegisterComponent]
public sealed partial class MobThresholdOffsetComponent : Component
{
    /// <summary>
    /// How much to increase the Dead damage threshold by.
    /// </summary>
    [DataField]
    public int DeadOffset = 0;

    /// <summary>
    /// How much to increase the Crit damage threshold by.
    /// </summary>
    [DataField]
    public int CritOffset = 0;
}
