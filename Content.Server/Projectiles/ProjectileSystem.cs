using Content.Server.Destructible;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Shared.Containers; // Forge-Change
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics; // Mono;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Projectiles;

public sealed partial class ProjectileSystem : SharedProjectileSystem
{
    [Dependency] private DestructibleSystem _destructibleSystem = default!;

    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private SharedContainerSystem _container = default!; // Forge-Change

    // <Mono>
    private EntityQuery<PhysicsComponent> _physQuery;
    private EntityQuery<FixturesComponent> _fixQuery;


    public override void Initialize()
    {
        base.Initialize();

        // Mono
        _physQuery = GetEntityQuery<PhysicsComponent>();
        _fixQuery = GetEntityQuery<FixturesComponent>();
        // Mono
        UpdatesBefore.Add(typeof(SharedPhysicsSystem));
    }

    public override DamageSpecifier? ProjectileCollide(Entity<ProjectileComponent, PhysicsComponent> projectile, EntityUid target, MapCoordinates? collisionCoordinates, bool predicted = false)
    {
        var (uid, component, ourBody) = projectile;
        // Check if projectile is already spent (server-specific check)
        if (component.ProjectileSpent)
            return null;

        var otherName = ToPrettyString(target);
        // Get damage required for destructible before base applies damage
        var damageRequired = FixedPoint2.Zero;
        if (TryComp<DamageableComponent>(target, out var damageableComponent))
        {
            damageRequired = _destructibleSystem.DestroyedAt(target);
            damageRequired -= damageableComponent.TotalDamage;
            damageRequired = FixedPoint2.Max(damageRequired, FixedPoint2.Zero);
        }
        // var deleted = Deleted(target); // Mono: Unused

        // Call base implementation to handle damage application and other effects
        var modifiedDamage = base.ProjectileCollide(projectile, target, collisionCoordinates, predicted);

        if (modifiedDamage == null)
        {
            // mono start
            if (!component.NoDamageDelete)
                return null;

            var spEv = new ProjectileSpentEvent();
            RaiseLocalEvent(uid, spEv);
            // mono end

            component.ProjectileSpent = true;
            if (component.DeleteOnCollide)
                QueueDel(uid);
            return null;
        }

        // Server-specific logic: penetration
        if (component.PenetrationThreshold != 0)
        {
            // If a damage type is required, stop the bullet if the hit entity doesn't have that type.
            if (component.PenetrationDamageTypeRequirement != null)
            {
                var stopPenetration = false;
                foreach (var requiredDamageType in component.PenetrationDamageTypeRequirement)
                {
                    if (!modifiedDamage.DamageDict.Keys.Contains(requiredDamageType))
                    {
                        stopPenetration = true;
                        break;
                    }
                }

                if (stopPenetration)
                    component.ProjectileSpent = true;
            }

            // If the object won't be destroyed, it "tanks" the penetration hit.
            if (modifiedDamage.GetTotal() < damageRequired)
            {
                component.ProjectileSpent = true;
            }

            if (!component.ProjectileSpent)
            {
                component.PenetrationAmount += damageRequired;
                // The projectile has dealt enough damage to be spent.
                if (component.PenetrationAmount >= component.PenetrationThreshold)
                {
                    component.ProjectileSpent = true;
                }
            }
        }
        else
        {
            component.ProjectileSpent = true;
        }

        // Mono
        if (component.ProjectileSpent)
        {
            var spEv = new ProjectileSpentEvent();
            RaiseLocalEvent(uid, spEv);
            if (component.DeleteOnCollide)
                QueueDel(uid);
        }

        return modifiedDamage;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ProjectileComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var projectileComp, out var physicsComp))
        {
            // Raycast only active in-flight projectiles. Dormant ammo in containers must not be
            // re-positioned because SetCoordinates will detach it from the container hierarchy.
            if (TerminatingOrDeleted(uid) ||
                !IsActiveProjectile(projectileComp) ||
                _container.IsEntityInContainer(uid))
                continue;

            var xform = Transform(uid);
            ApplyInFlightDamping(uid, projectileComp, physicsComp, xform, frameTime);

            // Damping may have landed the ammo; do not keep raycasting a dormant item.
            if (!IsActiveProjectile(projectileComp))
                continue;

            var currentVelocity = projectileComp.RaycastResetVelocity ?? _physics.GetMapLinearVelocity(uid, physicsComp, xform);
            var velLen = currentVelocity.Length();
            if (!ShouldRaycastProjectile(velLen) && projectileComp.RaycastResetVelocity == null)
                continue;

            var lastMap = _transformSystem.GetMapCoordinates(xform);
            var lastPosition = lastMap.Position;
            var rayDirection = currentVelocity / velLen;
            // Ensure rayDistance is not zero to prevent issues with IntersectRay if frametime or velocity is zero.
            var rayDistance = velLen * frameTime;
            if (rayDistance <= 0f)
                continue;

            if (!_fixQuery.TryComp(uid, out var fix) || !fix.Fixtures.TryGetValue(ProjectileFixture, out var projFix))
                continue;

            var hits = _physics.IntersectRay(xform.MapID,
                new CollisionRay(lastPosition, rayDirection, projFix.CollisionMask),
                rayDistance,
                uid, // Entity to ignore (self)
                false); // IncludeNonHard = false

            // do not process other grid velocity if we are gridded
            if (!ProcessHits(hits) && projectileComp.RaycastResetVelocity is { } resetVel)
            {
                var parentVel = _physics.GetMapLinearVelocity(xform.ParentUid);
                var resetTo = resetVel - parentVel;
                _physics.SetLinearVelocity(uid, resetTo, body: physicsComp);
                projectileComp.RaycastResetVelocity = null;
            }

            bool ProcessHits(IEnumerable<RayCastResults> hits)
            {
                // Process the closest hit
                // IntersectRay results are not guaranteed to be sorted by distance, so we go through them all.
                (EntityUid? Uid, float Distance) minHit = (null, float.MaxValue);
                foreach (var hit in hits)
                {
                    var hitEnt = hit.HitEntity;

                    if (!_physQuery.TryComp(hitEnt, out var otherBody) || !_fixQuery.TryComp(hitEnt, out var otherFix))
                        continue;

                    Fixture? hitFix = null;
                    foreach (var kv in otherFix.Fixtures)
                    {
                        if (kv.Value.Hard)
                        {
                            hitFix = kv.Value;
                            break;
                        }
                    }
                    if (hitFix == null)
                        continue;
                    // this is cursed but necessary
                    var ourEv = new PreventCollideEvent(uid, hitEnt, physicsComp, otherBody, projFix, hitFix);
                    RaiseLocalEvent(uid, ref ourEv);
                    if (ourEv.Cancelled)
                        continue;

                    var otherEv = new PreventCollideEvent(hitEnt, uid, otherBody, physicsComp, hitFix, projFix);
                    RaiseLocalEvent(hitEnt, ref otherEv);
                    if (otherEv.Cancelled)
                        continue;

                    if (hit.Distance < minHit.Distance)
                        minHit = (hitEnt, hit.Distance);
                }
                if (minHit.Uid == null)
                    return false;

                // teleport us so we hit it
                var hitXform = Transform(minHit.Uid.Value);
                var hitMapCoord = lastMap.Offset(rayDirection * minHit.Distance);
                var hitPos = _transformSystem.ToCoordinates(hitMapCoord);
                // if we somehow hit something not directly parented to space or a grid
                if (hitXform.Coordinates.EntityId != hitXform.GridUid && hitXform.GridUid != null)
                    hitPos = _transformSystem.WithEntityId(hitPos, hitXform.GridUid.Value);

                if (projectileComp.RaycastResetVelocity == null)
                {
                    var parentVel = _physics.GetMapLinearVelocity(xform.ParentUid);
                    projectileComp.RaycastResetVelocity = currentVelocity + parentVel; // record specifically world velocity
                    var curVel = physicsComp.LinearVelocity;
                    curVel.Normalize();
                    var resetTo = 1f / frameTime;
                    curVel *= resetTo;
                    _physics.SetLinearVelocity(uid, curVel, body: physicsComp);
                }

                _transformSystem.SetCoordinates(uid, hitPos);

                return true;
            }
        }
    }

    /// <summary>
    /// Below this map speed, damped ammo is forced to land instead of crawling forever.
    /// </summary>
    private const float InFlightStopSpeed = 0.2f;

    private void ApplyInFlightDamping(
        EntityUid uid,
        ProjectileComponent projectile,
        PhysicsComponent physics,
        TransformComponent xform,
        float frameTime)
    {
        if (projectile.InFlightLinearDampening <= 0f || frameTime <= 0f || projectile.RaycastResetVelocity != null)
            return;

        var mapVelocity = _physics.GetMapLinearVelocity(uid, physics, xform);
        var speedSq = mapVelocity.LengthSquared();
        if (speedSq <= 0f)
            return;

        var stopSpeedSq = InFlightStopSpeed * InFlightStopSpeed;
        if (speedSq <= stopSpeedSq)
        {
            DeactivateShotProjectile(uid, projectile);
            return;
        }

        var multiplier = MathF.Max(0f, 1f - projectile.InFlightLinearDampening * frameTime);
        if (multiplier >= 0.9999f)
            return;

        var dampedMapVelocity = mapVelocity * multiplier;
        if (dampedMapVelocity.LengthSquared() <= stopSpeedSq)
        {
            DeactivateShotProjectile(uid, projectile);
            return;
        }

        var parentVelocity = _physics.GetMapLinearVelocity(xform.ParentUid);
        _physics.SetLinearVelocity(uid, dampedMapVelocity - parentVelocity, body: physics);
    }
}
