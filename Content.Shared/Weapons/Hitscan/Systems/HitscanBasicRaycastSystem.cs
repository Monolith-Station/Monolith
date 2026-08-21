using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed partial class HitscanBasicRaycastSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ISharedAdminLogManager _log = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanBasicRaycastComponent, HitscanTraceEvent>(OnHitscanFired);
    }

    private void OnHitscanFired(Entity<HitscanBasicRaycastComponent> ent, ref HitscanTraceEvent args)
    {
        var shooter = args.Shooter ?? args.Gun;
        var mapCords = _transform.ToMapCoordinates(args.FromCoordinates);
        var ray = new CollisionRay(mapCords.Position, args.ShotDirection, (int) (ent.Comp.CollisionMask | ent.Comp.DiffuseLayers));
        var rayCastResults = _physics.IntersectRay(mapCords.MapId, ray, ent.Comp.MaxDistance, shooter, false);

        var target = args.Target;
        RayCastResults? result = null;
        var diffuseLayerCount = 0;
        foreach (var hit in rayCastResults)
        {
            if (!_physicQuery.TryComp(hit.HitEntity, out var phys))
                 continue;

             // Count diffusion lasers before target.
             if ((phys.CollisionLayer & (int) ent.Comp.DiffuseLayers) != 0)
            {
                diffuseLayerCount++;
                continue;
            }

             // Only entities on the normal collision mask can be the actual target.
            if ((phys.CollisionLayer & (int) ent.Comp.CollisionMask) == 0)
                 continue;

            // If you are in a container, use the first valid target.
            // Otherwise, preserve the existing target-selection rules.
            if (_container.IsEntityOrParentInContainer(shooter))
            {
                result = hit;
                break;
             }

            if (hit.HitEntity == target ||
                CompOrNull<RequireProjectileTargetComponent>(hit.HitEntity)?.Active != true)
            {
                result = hit;
                break;
            }
}

        var trace = new HitscanRaycastFiredEvent
        {
            FromCoordinates = args.FromCoordinates,
            ShotDirection = args.ShotDirection,
            Gun = args.Gun,
            Shooter = args.Shooter,
            HitEntities = [], // Mono
            DistanceTried = result?.Distance ?? ent.Comp.MaxDistance,
            DiffuseLayers = diffuseLayerCount,
        };

        if (result?.HitEntity != null) // Mono
        {
            trace.HitEntities.Add(result.Value.HitEntity);

            _log.Add(LogType.HitScanHit,
                $"{ToPrettyString(shooter):user} hit {ToPrettyString(result.Value.HitEntity):target}"
                + $" using {ToPrettyString(args.Gun):entity}.");
        }

        RaiseLocalEvent(ent, ref trace);
    }
}
