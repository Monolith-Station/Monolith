using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Mono.Traits.Physical;

/// <summary>
/// Grants slow, continuous regeneration to all present damage types.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PlateletFactoriesComponent : Component
{
    /// <summary>
    /// Seconds between regeneration ticks.
    /// </summary>
    [DataField]
    public float IntervalSeconds = 1f;

    /// <summary>
    /// Damage to apply per IntervalSeconds.
    /// </summary>
    [DataField]
    public DamageSpecifier DamagePerInterval = new();

    /// <summary>
    /// Multiplier applied to healing while the entity is in critical state.
    /// </summary>
    [DataField]
    public float CritMultiplier = 2f;

    /// <summary>
    /// The server time at which the next regeneration tick will occur.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;
}
