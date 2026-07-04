
namespace Content.Server._Mono.Projectiles.TargetSeeking;

/// <summary>
/// Raised on an entity that has become the target of an entity with a <see cref="TargetSeekingComponent"/>.  
/// </summary>
/// <remarks>
/// <see cref="TargetSeekingComponent.CurrentTarget"/> may not yet be changed to the entity this is directed at.
/// </remarks>
/// <param name="Seeker">The targeting entity.</param>
/// <param name="Exposed">Shares the value of <see cref="TargetSeekingComponent.ExposesTracking"/>, of the seeker.</param>
/// <seealso cref="TargetSeekingComponent.ExposesTracking"/>
[ByRefEvent]
public readonly record struct EntityStartedBeingSeekedTargetEvent(Entity<TargetSeekingComponent, TransformComponent> Seeker, bool Exposed = true);

/// <summary>
/// Raised on an entity that is no longer the target of an entity with a <see cref="TargetSeekingComponent"/>.
/// </summary>
/// <remarks>
/// This may be raised during shutdown of the seeker component, and <see cref="TargetSeekingComponent.CurrentTarget"/>
/// is not guaranteed to have been changed to a proper value yet.
/// </remarks>
/// <param name="Seeker">The targeting entity.</param>
/// <param name="Exposed">Shares the value of <see cref="TargetSeekingComponent.ExposesTracking"/>, of the seeker.</param>
/// <seealso cref="TargetSeekingComponent.ExposesTracking"/>
[ByRefEvent]
public readonly record struct EntityStoppedBeingSeekedTargetEvent(Entity<TargetSeekingComponent, TransformComponent> Seeker, bool Exposed = true);