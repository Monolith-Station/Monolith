namespace Content.Shared._Mono.Traits.Physical;

/// <summary>
/// Increases stamina and stamina regeneration.
/// </summary>
[RegisterComponent]
public sealed partial class VigorComponent : Component
{
    /// <summary>
    /// Flat bonus to stamina maximum (CritThreshold).
    /// </summary>
    [DataField]
    public float StaminaBonus = 10f;

    /// <summary>
    /// Bonus to stamina regeneration per second (Decay).
    /// </summary>
    [DataField]
    public float RegenerationBonus = 0.6f;

    /// <summary>
    /// Reduction to stamina cooldown (time before regen starts).
    /// </summary>
    [DataField]
    public float CooldownReduction = 0.75f; // 3s -> 2.25s = 3 * 0.75
}

