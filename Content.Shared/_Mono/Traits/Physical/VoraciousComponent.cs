using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Traits.Physical;

/// <summary>
/// Reduces food/drink action time by the multiplier.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VoraciousComponent : Component
{
    /// <summary>
    /// Divide the time to eat/drink by this value (e.g. 2.0 is twice as fast).
    /// </summary>
    [DataField]
    public float ConsumptionSpeedMultiplier = 2.0f;
}
