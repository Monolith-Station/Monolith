namespace Content.Shared._Mono.Detection;

/// <summary>
///     Component used on entities capable of detection that specifies their detection range.
/// </summary>
[RegisterComponent]
public sealed partial class DetectionRangeMultiplierComponent : Component
{
    /// <summary>
    ///     Multiplier of infrared detection radius.
    ///     Infrared detection radius scales with the square root of the grid's heat.
    /// </summary>
    [DataField]
    public float InfraredMultiplier = 1f;

    /// <summary>
    ///     In what portion of infrared detection radius to start showing the grid outline.
    /// </summary>
    [DataField]
    public float InfraredOutlinePortion = 0.6f;

    /// <summary>
    ///     Multiplier of visual detection radius.
    ///     Visual detection radius scales linearly off a grid's diagonal.
    /// </summary>
    [DataField]
    public float VisualMultiplier = 4f;

    /// <summary>
    ///     Whether to have effectively infinite detection range.
    ///     Use for aghosts.
    /// </summary>
    [DataField]
    public bool AlwaysDetect = false;
}
