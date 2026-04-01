using Content.Shared.Projectiles;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using System.Numerics;

namespace Content.Shared._Mono.Projectile;

public sealed partial class ProjectileKnockbackSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<PhysicsComponent> _physQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectileKnockbackComponent, ProjectileHitEvent>(OnHit);

        _physQuery = GetEntityQuery<PhysicsComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
    }

    private void OnHit(Entity<ProjectileKnockbackComponent> ent, ref ProjectileHitEvent args)
    {
        if (!_physQuery.TryComp(args.Target, out var targetBody)
            || !_physQuery.TryComp(ent, out var selfBody)
        )
            return;

        var toEnt = args.Target;
        if ((targetBody.BodyType & BodyType.Static) != 0)
            toEnt = Transform(toEnt).ParentUid;

        if (toEnt != args.Target && !_physQuery.TryComp(toEnt, out targetBody))
            return;

        var selfXform = Transform(ent);
        var selfCoord = new EntityCoordinates(ent, Vector2.Zero);
        var impulseCoord = _transform.WithEntityId(selfCoord, toEnt);

        // velocity is in world rotation frame so no need to translate it
        var dirVec = selfBody.LinearVelocity;
        dirVec.Normalize();

        var pos = impulseCoord.Position;
        // scale distance of application point to body center to scale rotation impulse
        pos = (pos - targetBody.LocalCenter) * ent.Comp.RotateMultiplier + targetBody.LocalCenter;
        _physics.ApplyLinearImpulse(toEnt, dirVec * ent.Comp.Knockback, pos);
    }
}
