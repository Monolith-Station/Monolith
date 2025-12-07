using System;

namespace Content.Server._Mono.StationEvents;

/// <summary>
/// Causes this station event to automatically extend its duration if there's players near any of the specified entities.
/// Also supports grids and automatically accounts for them potentially being big.
/// Supposed to be used by other systems and not YML.
/// Is removed once the event is ended.
/// </summary>
[RegisterComponent]
public sealed partial class AutoExtendRuleComponent : Component
{
    /// <summary>
    /// Entities for this to act on.
    /// Supposed to be filled by whatever system added this component.
    /// </summary>
    [DataField]
    public List<EntityUid> Entities = new();

    /// <summary>
    /// In what radius to check for players.
    /// </summary
    [DataField]
    public float PlayerCheckRadius = 512f;

    /// <summary>
    /// Extend the event if less than this much is left.
    /// </summary>
    [DataField]
    public TimeSpan ExtendAfterTime;

    /// <summary>
    /// By how much to extend the station event.
    /// Keep this above the recheck delay.
    /// </summary>
    [DataField]
    public TimeSpan ExtendBy;

    /// <summary>
    /// How often to perform the players-nearby check.
    /// Try to keep this high for performance reasons, but it shouldn't matter too much.
    /// </summary>
    [DataField]
    public TimeSpan RecheckDelay;

    [ViewVariables]
    public TimeSpan UpdateAccumulator = TimeSpan.FromSeconds(0);
}
