using Content.Server._Mono.Projectiles.TargetSeeking;

namespace Content.Server._Mono.TargetSeekingAlert;

/// <summary>
/// Raised on an entity with <see cref="TargetSeekerAlertComponent"/> when its grid gets targeted by its first target-seeker.
/// This is raised after this entity has been added to its grid's <see cref="TargetSeekerAlertGridComponent.Alerters"/>
/// and <see cref="TargetSeekerAlertGridComponent.ActiveAlerters"/>.
/// Raised before <see cref="TargetSeekerAlertStartedBeingTargetedEvent"/>, in the case that both this and that are raised at the same time. 
/// </summary>
[ByRefEvent]
public readonly record struct TargetSeekerAlertActivatedEvent();

/// <summary>
/// Raised on an entity with <see cref="TargetSeekerAlertComponent"/> when either the grid it is on has stopped being targeted by all
/// target-seekers, it has lost power, or otherwise been deactivated. This is raised after this entity has been removed from its grid's
/// <see cref="TargetSeekerAlertGridComponent.Alerters"/> and <see cref="TargetSeekerAlertGridComponent.ActiveAlerters"/>.
/// Raised after <see cref="TargetSeekerAlertStoppedBeingTargetedEvent"/>, in the case that both this and that are raised at the same time. 
/// </summary>
[ByRefEvent]
public readonly record struct TargetSeekerAlertDeactivatedEvent();


/// <summary>
/// Raised on an entity with <see cref="TargetSeekerAlertComponent"/> when the grid it's on gets targeted by a target-seeker, if the target-seeker's <see cref="TargetSeekingComponent.ExposesTracking"/> is true.
/// Raised after <see cref="TargetSeekerAlertActivatedEvent"/>, in the case that both this and that are raised at the same time. 
/// </summary>
/// <inheritdoc cref="EntityStartedBeingSeekedTargetEvent"/>
/// <param name="Active">Whether the grid was already being targeted by a target-seeker.</param>
[ByRefEvent]
public readonly record struct TargetSeekerAlertStartedBeingTargetedEvent(Entity<TargetSeekingComponent, TransformComponent> Seeker, bool Active);

/// <summary>
/// Raised on an entity with <see cref="TargetSeekerAlertComponent"/> when the grid it's on stops being targeted by any target-seekers.
/// Does not necessarily mean that no target-seekers are targeting this grid.
/// Raised before <see cref="TargetSeekerAlertDeactivatedEvent"/>, in the case that both this and that are raised at the same time. 
/// </summary>
/// <inheritdoc cref="EntityStoppedBeingSeekedTargetEvent"/>
/// <param name="Active">Whether the grid is still being targeted by atleast one target-seeker.</param>
[ByRefEvent]
public readonly record struct TargetSeekerAlertStoppedBeingTargetedEvent(Entity<TargetSeekingComponent, TransformComponent> Seeker, bool Active);
