using Content.Server.Destructible;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics; // Mono;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using System.Linq;
using System.Numerics;
using Content.Server.Gatherable.Components;

namespace Content.Server.Projectiles;

public sealed class ProjectileSystem : SharedProjectileSystem
{
    [Dependency] private readonly DestructibleSystem _destructibleSystem = default!;

    [Dependency] private readonly IMapManager _mapMan = default!; // Mono
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    // <Mono>
    private EntityQuery<PhysicsComponent> _physQuery;
    private EntityQuery<FixturesComponent> _fixQuery;

    /// <summary>
    /// Minimum velocity for a projectile to be considered for raycast hit detection.
    /// Projectiles slower than this will rely on standard StartCollideEvent.
    /// </summary>
    private const float MinRaycastVelocity = 75f;
    private const float RaycastResetVelocity = 20f; // velocity to reset to if we want to reset it
    private const float GridLookupRange = 6f;
    private List<Entity<MapGridComponent>> _grids = new();
    // </Mono>

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
        var deleted = Deleted(target);

        // Call base implementation to handle damage application and other effects
        var modifiedDamage = base.ProjectileCollide(projectile, target, collisionCoordinates, predicted);

        if (modifiedDamage == null)
        {
            // mono start
            if (!component.NoDamageDelete)
                return null;
            // mono end

            component.ProjectileSpent = true;
            if (component.DeleteOnCollide && component.ProjectileSpent)
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

        return modifiedDamage;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ProjectileComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var projectileComp, out var physicsComp))
        {
            if (projectileComp.ProjectileSpent || TerminatingOrDeleted(uid))
                continue;

            var currentVelocity = physicsComp.LinearVelocity;
            var velLen = currentVelocity.Length();
            if (velLen < MinRaycastVelocity)
                continue;

            var xform = Transform(uid);
            var lastPosition = _transformSystem.GetWorldPosition(xform);
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
            if (ProcessHits(hits) || xform.GridUid != null)
                continue;

            // no hit, but a grid might still phase into *us*
            var rayBox = new Box2(new Vector2(-GridLookupRange, -GridLookupRange) + lastPosition,
                                    new Vector2(GridLookupRange, rayDistance + GridLookupRange) + lastPosition);

            var rayBoxRotated = new Box2Rotated(rayBox, rayDirection.ToWorldAngle() - new Angle(Math.PI), lastPosition);
            _grids.Clear();
            _mapMan.FindGridsIntersecting(xform.MapID, rayBoxRotated, ref _grids);

            // raycast but in terms relative to the grid, basically: temporarily pretend we have -gridVel velocity added to us, and the grid is stationary
            foreach (var grid in _grids)
            {
                if (!_physQuery.TryComp(grid, out var gridBody))
                    continue;

                var gridVel = gridBody.LinearVelocity;
                var relVel = currentVelocity - gridVel;
                // raycast from us into the grid
                var relVelLen = relVel.Length();
                if (relVelLen < MinRaycastVelocity)
                    continue;

                var gridRayDir = relVel / relVelLen;
                var gridRayLen = relVelLen * frameTime;

                var gridHits = _physics.IntersectRay(xform.MapID,
                    new CollisionRay(lastPosition, gridRayDir, projFix.CollisionMask),
                    gridRayLen,
                    uid, // Entity to ignore (self)
                    false); // IncludeNonHard = false

                if (ProcessHits(gridHits, grid))
                    break;
            }

            bool ProcessHits(IEnumerable<RayCastResults> hits, EntityUid? gridNeeded = null)
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

                    var thisHitXform = Transform(hitEnt);
                    if (gridNeeded != null && thisHitXform.GridUid != gridNeeded)
                        continue;

                    if (hit.Distance < minHit.Distance)
                        minHit = (hitEnt, hit.Distance);
                }
                if (minHit.Uid == null)
                    return false;

                // teleport us so we hit it
                // this is cursed but i don't think there's a better way to force a collision here
                var hitXform = Transform(minHit.Uid.Value);
                var hitPos = hitXform.Coordinates;
                // if we somehow hit something not directly parented to space or a grid
                if (hitXform.Coordinates.EntityId != hitXform.GridUid && hitXform.GridUid != null)
                    hitPos = _transformSystem.WithEntityId(hitPos, hitXform.GridUid.Value);

                _transformSystem.SetCoordinates(uid, hitPos);
                if (projectileComp.RaycastResetVelocity)
                    _physics.SetLinearVelocity(uid, rayDirection * RaycastResetVelocity);

                return true;
            }
        }
    }
}
