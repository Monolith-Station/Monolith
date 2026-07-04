namespace Content.Shared._Mono.Detection;

/// <summary>
///     Component used on entities capable of being detected that specifies their detected-at range multipliers.
///     For docs on the fields that don't have them here, <see cref="DetectionRangeMultiplierComponent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class DetectedAtRangeMultiplierComponent : Component
{
    [DataField]
    public float InfraredMultiplier = 1f;

    [DataField]
    public float VisualMultiplier = 1f;

    /// <summary>
    ///     Flat value to add to visual detected-at radius, AFTER all multipliers.
    ///     Use for asteroids and debris.
    /// </summary>
    [DataField]
    public float VisualBias = 0f;
}
