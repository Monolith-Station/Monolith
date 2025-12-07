using System.Numerics;
using Content.Shared.Interaction;
using Content.Server.Shuttles.Components;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Mono.Projectiles.TargetSeeking;

/// <summary>
///     Handles the logic for target-seeking projectiles.
/// </summary>
public sealed class TargetSeekingSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = null!;
    [Dependency] private readonly RotateToFaceSystem _rotateToFace = null!;
    [Dependency] private readonly PhysicsSystem _physics = null!;
    [Dependency] private readonly IGameTiming _gameTiming = default!; // Mono

    private EntityQuery<ProjectileComponent> _projectileQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        base.Initialize();

        _projectileQuery = GetEntityQuery<ProjectileComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();

        SubscribeLocalEvent<TargetSeekingComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<TargetSeekingComponent, EntParentChangedMessage>(OnParentChanged);

        SubscribeLocalEvent<TargetSeekingComponent, ComponentShutdown>(OnTargetSeekingShutdown);
    }

    private void OnTargetSeekingShutdown(Entity<TargetSeekingComponent> seekerEntity, ref ComponentShutdown args)
    {
        if (seekerEntity.Comp.CurrentTarget is { } oldTargetEntity)
            OnChangingSeekingTarget(seekerEntity, oldTargetEntity);
    }

    /// <summary>
    /// Called on a seeker when its <see cref="TargetSeekingComponent.CurrentTarget"/> is changed, directed at the new target.
    /// </summary>
    private void OnStartingSeeking(Entity<TargetSeekingComponent, TransformComponent?> seekerTransformEntity, EntityUid newTargetUid)
    {
        if (!Resolve(seekerTransformEntity, ref seekerTransformEntity.Comp2))
            return;

        var startedSeekingEvent = new EntityStartedBeingSeekedTargetEvent(seekerTransformEntity!, seekerTransformEntity.Comp1.ExposesTracking);
        RaiseLocalEvent(newTargetUid, ref startedSeekingEvent);
    }

    /// <summary>
    /// Called on a seeker when it either loses or changes its <see cref="TargetSeekingComponent.CurrentTarget"/>, directed at the old target.
    /// </summary>
    private void OnChangingSeekingTarget(Entity<TargetSeekingComponent, TransformComponent?> seekerTransformEntity, EntityUid oldTargetUid)
    {
        if (!Resolve(seekerTransformEntity, ref seekerTransformEntity.Comp2))
            return;

        // because you shouldn't be calling this outside of SetSeekerTarget and OnTargetSeekingShutdown improperly
        DebugTools.AssertNotNull(seekerTransformEntity.Comp1.CurrentTarget, "When raising EntityStoppedBeingSeekedTarget, CurrentTarget was already set to null!");

        var changedSeekingEvent = new EntityStoppedBeingSeekedTargetEvent(seekerTransformEntity!, seekerTransformEntity.Comp1.ExposesTracking);
        RaiseLocalEvent(oldTargetUid, ref changedSeekingEvent);
    }

    /// <summary>
    /// Sets a target-seeking projectile's <see cref="TargetSeekingComponent.CurrentTarget"/>, and raises
    /// the appropriate events. 
    /// </summary>
    // NOTE: In the future, someone could want to change this to separate whether `CurrentTarget` is null with whether the seeker is actually targeting something.
    //       If so, change this to take in whether the seeker should be targeting something, rather than whether the target exists.
    //       Then, you'd be free to set `CurrentTarget` without needing to use this function.. ideally.
    public void SetSeekerTarget(Entity<TargetSeekingComponent> seekerEntity, EntityUid? targetUid, TransformComponent? seekerTransform = null)
    {
        var (_, seekerComponent) = seekerEntity;

        // if the new target is different from the old target,
        if (seekerComponent.CurrentTarget != targetUid)
        {
            // and we had an old target, then raise changing-seeking
            if (seekerComponent.CurrentTarget is { } oldTargetUid)
                OnChangingSeekingTarget((seekerEntity, seekerComponent, seekerTransform), oldTargetUid);

            // and our new target isn't null, then raise starting-seeking
            if (targetUid != null)
                OnStartingSeeking((seekerEntity, seekerComponent, seekerTransform), targetUid.Value);
        }

        seekerComponent.CurrentTarget = targetUid;
    }

    /// <summary>
    /// Called when a target-seeking projectile hits something.
    /// </summary>
    private void OnProjectileHit(EntityUid uid, TargetSeekingComponent component, ref ProjectileHitEvent args)
    {
        // If we hit our actual target, we could perform additional effects here
        if (component.CurrentTarget.HasValue && component.CurrentTarget.Value == args.Target)
        {
            // Target hit successfully
        }

        // Reset the target since we've hit something
        SetSeekerTarget((uid, component), null);
    }

    /// <summary>
    /// Called when a target-seeking projectile changes parent (e.g., enters a grid).
    /// </summary>
    private void OnParentChanged(Entity<TargetSeekingComponent> seekerEntity, ref EntParentChangedMessage args)
    {
        // Check if the projectile has entered a grid
        if (args.Transform.GridUid == null)
            return;

        // Get the shooter's grid to compare
        if (!_projectileQuery.TryGetComponent(seekerEntity.Owner, out var projectile) ||
            !TryComp(projectile.Shooter, out TransformComponent? shooterTransform))
            return;

        var shooterGridUid = shooterTransform.GridUid;
        var currentGridUid = args.Transform.GridUid;

        // If we've entered a different grid than the shooter's grid, disable seeking
        if (currentGridUid != shooterGridUid)
            seekerEntity.Comp.SeekingDisabled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var ticktime = _gameTiming.TickPeriod;

        var query = EntityQueryEnumerator<TargetSeekingComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var seekingComp, out var body, out var xform))
        {
            var acceleration = seekingComp.Acceleration * frameTime;
            // Initialize launch speed.
            if (seekingComp.Launched == false)
            {
                acceleration += seekingComp.LaunchSpeed;
                seekingComp.Launched = true;
            }

            // Apply acceleration in the direction the projectile is facing
            _physics.SetLinearVelocity(uid, body.LinearVelocity + _transform.GetWorldRotation(xform).ToWorldVec() * acceleration, body: body);

            var velLen = body.LinearVelocity.Length();
            // cut off velocity above max
            if (velLen > seekingComp.MaxSpeed)
                _physics.SetLinearVelocity(uid, body.LinearVelocity * (seekingComp.MaxSpeed / velLen), body: body);

            // Skip seeking behavior if disabled (e.g., after entering an enemy grid)
            if (seekingComp.SeekingDisabled)
                continue;

            if (seekingComp.TrackDelay > 0f)
            {
                seekingComp.TrackDelay -= frameTime;
                continue;
            }

            // If we have a target, track it using the selected algorithm
            if (seekingComp.CurrentTarget.HasValue && !TerminatingOrDeleted(seekingComp.CurrentTarget))
            {
                var target = seekingComp.CurrentTarget.Value;
                if (!_physicsQuery.TryGetComponent(target, out var targetBody))
                    continue;

                var targetXform = Transform(target);

                Angle wantAngle = new Angle(0);
                switch (seekingComp.TrackingAlgorithm)
                {
                    case TrackingMethod.Direct:
                        wantAngle = ApplyDirectTracking((uid, xform), (target, targetXform), frameTime); break;
                    case TrackingMethod.Predictive:
                        wantAngle = ApplyPredictiveTracking((uid, seekingComp, body, xform), (target, targetBody, targetXform), frameTime); break;
                    case TrackingMethod.AdvancedPredictive:
                        wantAngle = ApplyAdvancedTracking((uid, seekingComp, body, xform), (target, targetBody, targetXform), frameTime); break;
                }

                _rotateToFace.TryRotateTo(
                    uid,
                    wantAngle,
                    frameTime,
                    seekingComp.Tolerance,
                    seekingComp.TurnRate?.Theta ?? MathF.PI * 2,
                    xform
                );
            }
            else
            {
                // Try to acquire a new target
                AcquireTarget(uid, seekingComp, xform);
            }
        }
    }

    /// <summary>
    /// Finds the closest valid target within range and tracking parameters.
    /// </summary>
    public void AcquireTarget(EntityUid uid, TargetSeekingComponent component, TransformComponent transform)
    {
        var closestDistance = float.MaxValue;
        EntityUid? bestTarget = null;

        // Look for shuttles to target
        var shuttleQuery = EntityQueryEnumerator<ShuttleConsoleComponent>();

        while (shuttleQuery.MoveNext(out var targetUid, out _))
        {
            var targetXform = Transform(targetUid);

            // If this entity has a grid UID, use that as our actual target
            // This targets the ship grid rather than just the console
            var actualTarget = targetXform.GridUid ?? targetUid;

            // Get angle to the target
            var targetPos = _transform.ToMapCoordinates(targetXform.Coordinates).Position;
            var sourcePos = _transform.ToMapCoordinates(transform.Coordinates).Position;
            var angleToTarget = (targetPos - sourcePos).ToWorldAngle();

            // Get current direction of the projectile
            var currentRotation = _transform.GetWorldRotation(transform);

            // Check if target is within field of view
            var angleDifference = Angle.ShortestDistance(currentRotation, angleToTarget).Degrees;
            if (MathF.Abs((float)angleDifference) > component.ScanArc / 2)
            {
                continue; // Target is outside our field of view
            }

            // Calculate distance to target
            var distance = Vector2.Distance(sourcePos, targetPos);

            // Skip if target is out of range
            if (distance > component.DetectionRange)
                continue;

            // Skip if the target is our own launcher (don't target our own ship)
            if (_projectileQuery.TryGetComponent(uid, out var projectile) &&
                TryComp(projectile.Shooter, out TransformComponent? shooterTransform))
            {
                var shooterGridUid = shooterTransform.GridUid;

                // If the shooter is on the same grid as this potential target, skip it
                if (targetXform.GridUid.HasValue && shooterGridUid == targetXform.GridUid)
                {
                    continue;
                }
            }

            // If this is closer than our previous best target, update
            if (closestDistance > distance)
            {
                closestDistance = distance;
                bestTarget = actualTarget;
            }
        }

        // Set our new target
        if (bestTarget.HasValue)
            SetSeekerTarget((uid, component), bestTarget, transform);
    }

    /// <summary>
    /// Advanced tracking that predicts where the target will be based on its velocity.
    /// </summary>
    public Angle ApplyPredictiveTracking(Entity<TargetSeekingComponent, PhysicsComponent, TransformComponent> ent, Entity<PhysicsComponent, TransformComponent> target, float frameTime)
    {
        // Get current positions
        var currentTargetPosition = _transform.GetWorldPosition(target.Comp2);
        var sourcePosition = _transform.GetWorldPosition(ent.Comp3);

        // Calculate current distance
        var toTargetVec = currentTargetPosition - sourcePosition;
        var currentDistance = toTargetVec.Length();

        var targetVelocity = _physics.GetMapLinearVelocity(target, target.Comp1, target.Comp2);
        var ourVelocity = _physics.GetMapLinearVelocity(ent, ent.Comp2, ent.Comp3);
        var relVel = ourVelocity - targetVelocity;

        // Calculate time to intercept (using closing rate)
        var closingRate = Vector2.Dot(relVel, toTargetVec) / toTargetVec.Length();
        var timeToIntercept = currentDistance / closingRate;

        // Prevent negative or very small intercept times that could cause erratic behavior
        timeToIntercept = MathF.Max(timeToIntercept, 0.1f);

        // Predict where the target will be when we reach it
        var predictedPosition = currentTargetPosition + (targetVelocity * timeToIntercept);

        // Calculate angle to the predicted position
        var targetAngle = (predictedPosition - sourcePosition).ToWorldAngle();

        return targetAngle;
    }

    /// <summary>
    /// More advanced and accurate tracking.
    /// Works best for missiles with low friction and high max speed, where they spend all or most of their lifetime accelerating and being under max speed.
    /// </summary>
    // see: https://github.com/Ilya246/orbitfight/blob/master/src/entities.cpp for original
    public Angle ApplyAdvancedTracking(Entity<TargetSeekingComponent, PhysicsComponent, TransformComponent> ent, Entity<PhysicsComponent, TransformComponent> target, float frameTime)
    {
        const int guidanceIterations = 3;

        var accel = ent.Comp1.Acceleration;

        var ownVel = _physics.GetMapLinearVelocity(ent, ent.Comp2, ent.Comp3);
        var ownPos = _transform.GetWorldPosition(ent.Comp3);
        var targetVel = _physics.GetMapLinearVelocity(target, target.Comp1, target.Comp2);
        var targetPos = _transform.GetWorldPosition(target.Comp2);
        var relVel = targetVel - ownVel;
        var relPos = targetPos - ownPos;

        var dVx = relVel.X;
        var dVy = relVel.Y;
        var dX = relPos.X;
        var dY = relPos.Y;
        var refRot = MathF.Atan2(dVy, dVx);
        var vel = dVx / MathF.Cos(refRot);
        var projX = dX * MathF.Cos(refRot) + dY * MathF.Sin(refRot);
        var projY = dY * MathF.Cos(refRot) - dX * MathF.Sin(refRot);
        var itime = GuessInterceptTime(0f, -projX, -vel, projY, accel);
        for (var i = 0; i < guidanceIterations; i++)
            itime = GuessInterceptTime(itime, -projX, -vel, projY, accel);

        var targetRot = (relPos + relVel * itime).ToWorldAngle();

        return targetRot;

        // the explanation for how this works would take more space than the enclosing method so it's not included here
        float GuessInterceptTime(float prev, float x0, float vel, float y0, float accel)
        {
            var x = x0 + vel * prev;
            var d = MathF.Sqrt(x * x + y0 * y0);
            var dd = vel * x / d;
            return (dd + MathF.Sqrt(dd * dd + 2f * accel * (d - dd * prev))) / (accel);
        }
    }

    /// <summary>
    /// Basic tracking that points directly at the current target position.
    /// </summary>
    public Angle ApplyDirectTracking(Entity<TransformComponent> ent, Entity<TransformComponent> target, float frameTime)
    {
        // Get the angle directly toward the target
        var angleToTarget = (_transform.GetWorldPosition(target.Comp) - _transform.GetWorldPosition(ent.Comp)).ToWorldAngle();

        return angleToTarget;
    }
}
