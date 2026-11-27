using System.Numerics;
using Content.Shared._Mono.Weapons.Hitscan.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Hitscan.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Mono.Weapons.Hitscan.Systems;

public sealed partial class HitscanMultiRaycastSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private ISharedAdminLogManager _log = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private HitscanBasicRaycastSystem _basicTrace = default!;

    [Dependency] private EntityQuery<PhysicsComponent> _physicQuery = default!;
    private HashSet<EntityUid> _hitEntities = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanMultiRaycastComponent, HitscanTraceEvent>(OnHitscanFired);
    }

    private void OnHitscanFired(Entity<HitscanMultiRaycastComponent> ent, ref HitscanTraceEvent args)
    {
        var shooter = args.Shooter ?? args.Gun;
        var mapCords = _transform.ToMapCoordinates(args.FromCoordinates);
        Trace(ent.Comp, mapCords, args.ShotDirection, shooter, out var shield, out var distance, _hitEntities);
        foreach (var target in _hitEntities)
        {
            _log.Add(LogType.HitScanHit,
                $"{ToPrettyString(shooter):user} hit {ToPrettyString(target):target}"
                + $" using {ToPrettyString(args.Gun):entity}.");
        }

        var trace = new HitscanRaycastFiredEvent
        {
            FromCoordinates = args.FromCoordinates,
            ShotDirection = args.ShotDirection,
            Gun = args.Gun,
            Shooter = args.Shooter,
            HitEntities = _hitEntities,
            DistanceTried = distance,
        };

        RaiseLocalEvent(ent, ref trace);
        _hitEntities.Clear();
        if (!trace.Canceled && shield.IsValid())
            _basicTrace.PublishShieldHit(ent.Owner, shield, shooter, mapCords.Offset(args.ShotDirection * distance));
    }

    public void Trace(HitscanMultiRaycastComponent settings, MapCoordinates start, Vector2 direction, EntityUid shooter,
        out EntityUid shield, out float distance, HashSet<EntityUid>? hits = null)
    {
        var limit = _basicTrace.ShieldRange(start, direction, shooter, settings.MaxDistance, out shield);
        var ray = new CollisionRay(start.Position, direction, (int) settings.CollisionMask);
        distance = limit;
        var hitCount = 0;
        foreach (var result in _physics.IntersectRay(start.MapId, ray, limit, shooter, false))
        {
            if (_basicTrace.IsShieldProxy(result) || !_physicQuery.TryComp(result.HitEntity, out var phys))
                continue;

            hits?.Add(result.HitEntity);
            distance = result.Distance;
            if (++hitCount > settings.MaxPierce || (phys.CollisionLayer & (int) settings.PierceCollisionMask) != 0)
            {
                shield = EntityUid.Invalid;
                return;
            }
        }

        if (shield.IsValid())
            distance = limit;
    }
}
