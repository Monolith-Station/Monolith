using Content.Server.Destructible;
using Content.Server._Mono.Ships; // Mono
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
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
    [Dependency] private ShipShieldCollisionSystem _shipShields = default!; // Mono

    protected override void OnShieldContact(Entity<ProjectileComponent> projectile, ref StartCollideEvent args) // Mono
        => _shipShields.ConsumeProjectile(projectile, ref args);

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
        UpdatesAfter.Add(typeof(ShipShieldCollisionSystem)); // Mono
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
            if (projectileComp.ProjectileSpent || TerminatingOrDeleted(uid))
            {
                projectileComp.RaycastResetVelocity = null; // Mono
                continue;
            }

            var xform = Transform(uid);
            if (TryComp<CEZShotComponent>(uid, out var zShot) && zShot.Crossed)
            {
                if (zShot.RestoreVelocity is { } velocity)
                    SetZShotMapVelocity(uid, xform, physicsComp, velocity);
                RemComp<CEZShotComponent>(uid);
                zShot = null;
            }

            var mapVelocity = _physics.GetMapLinearVelocity(uid, physicsComp, xform);
            var parentVel = mapVelocity - physicsComp.LinearVelocity;
            var currentVelocity = projectileComp.RaycastResetVelocity ?? (zShot != null ? mapVelocity : physicsComp.LinearVelocity);
            var velLen = currentVelocity.Length();
            var raycast = ShouldRaycastProjectile(velLen) || projectileComp.RaycastResetVelocity != null;
            if (!raycast && zShot == null && !_shipShields.HasShells) // Mono
                continue;

            var lastMap = _transformSystem.GetMapCoordinates(xform);
            var lastPosition = lastMap.Position;
            // Ensure rayDistance is not zero to prevent issues with IntersectRay if frametime or velocity is zero.
            var rayDistance = velLen * frameTime;
            if (rayDistance <= 0f)
                continue;

            var rayDirection = currentVelocity / velLen;
            var fraction = 0f;
            var destination = default(MapCoordinates);
            var crosses = zShot != null && TryGetZShotCrossing((uid, zShot), xform, lastPosition,
                currentVelocity * frameTime, out fraction, out destination);
            if (!crosses && !raycast && !_shipShields.HasShells) // Mono
                continue;

            if (!_fixQuery.TryComp(uid, out var fix) || !fix.Fixtures.TryGetValue(ProjectileFixture, out var projFix))
                continue;

            var sourceDistance = crosses ? rayDistance * fraction : rayDistance;
            if (Cast(sourceDistance))
                continue;

            if (crosses && zShot != null)
            {
                if (_shipShields.TryBlockProjectileCrossing((uid, projectileComp), destination)) // Mono
                    continue;

                // Resolve collisions on the source deck before moving, then sweep the rest of the tick on the destination.
                var rotation = _transformSystem.GetWorldRotation(xform);
                _transformSystem.SetMapCoordinates((uid, xform), destination);
                _transformSystem.SetWorldRotation(uid, rotation);
                SetZShotMapVelocity(uid, xform, physicsComp, currentVelocity);
                zShot.Crossed = true;
                projectileComp.RaycastResetVelocity = null;
                lastMap = destination;
                lastPosition = destination.Position;
                if (!Cast(rayDistance - sourceDistance))
                {
                    zShot.RestoreVelocity = currentVelocity;
                    SetZShotMapVelocity(uid, xform, physicsComp, currentVelocity * (1f - fraction));
                }
                continue;
            }

            // do not process other grid velocity if we are gridded
            if (projectileComp.RaycastResetVelocity is { } resetVel)
            {
                _physics.SetLinearVelocity(uid, resetVel - parentVel, body: physicsComp);
                projectileComp.RaycastResetVelocity = null;
            }

            bool Cast(float distance)
            {
                // Mono Start
                if (distance <= 0f)
                    return false;

                var blocked = _shipShields.TryFindProjectileHit(uid, lastMap, rayDirection, distance, out var shield, out var shieldDistance);
                if ((raycast || crosses || blocked) && ProcessHits(_physics.IntersectRay(lastMap.MapId,
                        new CollisionRay(lastPosition, rayDirection, projFix.CollisionMask), blocked ? shieldDistance : distance, uid, false)))
                    return true;

                if (blocked)
                    _shipShields.ConsumeProjectile((uid, projectileComp), shield, lastPosition + rayDirection * shieldDistance);

                return blocked;
                // Mono End
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
                    projectileComp.RaycastResetVelocity = zShot != null
                        ? currentVelocity
                        : currentVelocity + parentVel; // record specifically world velocity
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
}
