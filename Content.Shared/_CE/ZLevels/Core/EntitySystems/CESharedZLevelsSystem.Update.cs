/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Components;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    public int UpdateCalls { get; private set; }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateCalls = 0;

        if (_net.IsClient && !_clientSimulation)
            return;

        UpdateZPhysics(frameTime);
    }

    private void UpdateZPhysics(float frameTime)
    {
        UpdateDirtyMovement();

        for (var i = _activeBodies.Count - 1; i >= 0; i--)
        {
            var uid = _activeBodies[i];

            if (!ZPhysicsQuery.TryComp(uid, out var zPhysicsComponent) ||
                !_transformQuery.TryComp(uid, out var xform) ||
                !_physicsQuery.TryComp(uid, out var physics))
            {
                _activeBodies.RemoveAt(i);
                continue;
            }

            if (!_zMapQuery.HasComp(xform.MapUid))
            {
                _activeBodies.RemoveAt(i);
                continue;
            }

            // Mono: The client only handles predicted stuff (non-predicted is the server's problem)
            if (_net.IsClient && !physics.Predict)
                continue;

            ProcessZPhysics((uid, zPhysicsComponent, physics), frameTime);
        }
    }

    private void ProcessZPhysics(Entity<CEZPhysicsComponent, PhysicsComponent> entity, float frameTime)
    {
        UpdateCalls++;

        var zPhysicsComponent = entity.Comp1;

        var oldVelocity = zPhysicsComponent.Velocity;
        var oldHeight = zPhysicsComponent.LocalPosition;

        if (zPhysicsComponent.VelocityGravity)
            zPhysicsComponent.Velocity -= ZGravityForce * zPhysicsComponent.GravityMultiplier * frameTime;

        if (zPhysicsComponent.VelocityRaiseEvent)
        {
            var velocityEvent = new CEGetZVelocityEvent((entity, zPhysicsComponent));
            RaiseLocalEvent(entity, ref velocityEvent);

            zPhysicsComponent.Velocity += velocityEvent.VelocityDelta * frameTime;
        }

        zPhysicsComponent.LocalPosition += zPhysicsComponent.Velocity * frameTime;
        var distanceToGround = zPhysicsComponent.LocalPosition - zPhysicsComponent.CachedGroundHeight;

        if (zPhysicsComponent.AutoStep && distanceToGround < 0)
            zPhysicsComponent.LocalPosition -= distanceToGround;

        if (zPhysicsComponent.CachedStickyGround)
            zPhysicsComponent.LocalPosition -= distanceToGround;

        if (zPhysicsComponent is { Velocity: < 0, Fallable: true })
        {
            if (distanceToGround <= 0.05f)
            {
                if (float.Abs(zPhysicsComponent.Velocity) >= ImpactVelocityLimit)
                {
                    var hitEv = new CEZLevelHitEvent(-zPhysicsComponent.Velocity);
                    RaiseLocalEvent(entity, ref hitEv);

                    var land = new LandEvent(null, true);
                    RaiseLocalEvent(entity, ref land);
                }

                if (float.Abs(zPhysicsComponent.Velocity) < zPhysicsComponent.SleepThreshold)
                {
                    zPhysicsComponent.Velocity = 0;
                    zPhysicsComponent.LocalPosition = zPhysicsComponent.CachedGroundHeight;
                }
                else
                {
                    zPhysicsComponent.Velocity = -zPhysicsComponent.Velocity * zPhysicsComponent.Bounciness;
                }
            }
        }

        if (zPhysicsComponent.LocalPosition < 0)
        {
            if (TryMoveDownOrChasm(entity))
            {
                zPhysicsComponent.LocalPosition += 1;
                if (zPhysicsComponent is { CachedStickyGround: false, Fallable: true })
                {
                    var fallEv = new CEZLevelFallMapEvent();
                    RaiseLocalEvent(entity, ref fallEv);
                }
            }
        }

        if (zPhysicsComponent.LocalPosition >= 1)
        {
            if (HasTileAbove(entity))
            {
                if (float.Abs(zPhysicsComponent.Velocity) >= ImpactVelocityLimit)
                {
                    var hitEv = new CEZLevelHitEvent(zPhysicsComponent.Velocity);
                    RaiseLocalEvent(entity, ref hitEv);

                    var land = new LandEvent(null, true);
                    RaiseLocalEvent(entity, ref land);
                }
                zPhysicsComponent.LocalPosition = 1;
                zPhysicsComponent.Velocity = -zPhysicsComponent.Velocity * zPhysicsComponent.Bounciness;
            }
            else
            {
                if (TryMoveUp(entity))
                    zPhysicsComponent.LocalPosition -= 1;
            }
        }

        if (float.Abs(zPhysicsComponent.Velocity) > ZVelocityLimit)
            zPhysicsComponent.Velocity = float.Sign(zPhysicsComponent.Velocity) * ZVelocityLimit;

        if (float.Abs(oldVelocity - zPhysicsComponent.Velocity) > 0.001f)
            DirtyField(entity, zPhysicsComponent, nameof(CEZPhysicsComponent.Velocity));

        if (float.Abs(oldHeight - zPhysicsComponent.LocalPosition) > 0.001f)
            DirtyField(entity, zPhysicsComponent, nameof(CEZPhysicsComponent.LocalPosition));

        if (zPhysicsComponent.VelocityGravity)
        {
            var targetStatus = distanceToGround > AirborneHeightThreshold ? BodyStatus.InAir : BodyStatus.OnGround;
            if (entity.Comp2.BodyStatus != targetStatus)
            {
                _physicsSystem.SetBodyStatus(entity, entity.Comp2, targetStatus);
                var statusEv = new CEZBodyStatusChangedEvent(targetStatus);
                RaiseLocalEvent(entity, ref statusEv);
            }
        }

        SleepUpdate((entity, entity.Comp1), frameTime);
    }

    private void SleepUpdate(Entity<CEZPhysicsComponent> entity, float frameTime)
    {
        var distanceToGround = entity.Comp.LocalPosition - entity.Comp.CachedGroundHeight;
        var almostStopped = float.Abs(entity.Comp.Velocity) < entity.Comp.SleepThreshold && float.Abs(distanceToGround) <= 0.01f;

        if (!almostStopped)
        {
            entity.Comp.SleepTimer = 0f;
            return;
        }

        entity.Comp.SleepTimer += frameTime;
        if (entity.Comp.SleepTimer < entity.Comp.TimeToSleep)
            return;

        SleepBody((entity, entity));
    }
}

/// <summary>
/// Raised directed on an entity when its BodyStatus changes due to Z-physics height sync.
/// </summary>
[ByRefEvent]
public readonly record struct CEZBodyStatusChangedEvent(BodyStatus NewStatus);
