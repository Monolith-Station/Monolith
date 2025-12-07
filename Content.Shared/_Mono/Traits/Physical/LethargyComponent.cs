namespace Content.Shared._Mono.Traits.Physical;

/// <summary>
/// Decreases stamina and stamina regeneration and increases the cooldown before stamina starts regenerating.
/// </summary>
[RegisterComponent]
public sealed partial class LethargyComponent : Component
{
    /// <summary>
    /// Flat penalty to stamina maximum (CritThreshold).
    /// </summary>
    [DataField]
    public float StaminaPenalty = 15f;

    /// <summary>
    /// Penalty to stamina regeneration per second (Decay).
    /// </summary>
    [DataField]
    public float RegenerationPenalty = 0.6f;

    /// <summary>
    /// Multiplier applied to stamina cooldown. 1.25 turns 3.0 seconds into 3.75 seconds.
    /// </summary>
    [DataField]
    public float CooldownIncrease = 1.25f;
}


