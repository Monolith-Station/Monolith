namespace Content.Server._Mono.Cleanup;

/// <summary>
///     Stores at which time will we have to be still meeting cleanup conditions for this grid to get cleaned up.
/// </summary>
[RegisterComponent]
public sealed partial class GridCleanupGridComponent : Component
{
    [ViewVariables]
    public TimeSpan CleanupAccumulator = TimeSpan.FromSeconds(0);

    /// <summary>
    ///     If set, will make this grid get cleaned up faster or slower. 3x means 3 times less time to get cleaned up.
    /// </summary>
    [DataField]
    public float CleanupAcceleration = 1f;

    /// <summary>
    ///     If set, will make this grid get cleaned up if no players are in this range instead of default.
    /// </summary>
    [DataField]
    public float? DistanceOverride = null;

    /// <summary>
    ///     Whether to cleanup this grid even if it's powered.
    /// </summary>
    [DataField]
    public bool IgnorePowered = false;

    /// <summary>
    ///     Whether to cleanup this grid even if it's expensive.
    /// </summary>
    [DataField]
    public bool IgnorePrice = false;

    /// <summary>
    ///     Whether to cleanup this grid even if it has IFF on.
    /// </summary>
    [DataField]
    public bool IgnoreIFF = false;
}
