using System.Linq;
using Content.Shared._Crescent.ShipShields;
using System.Numerics; // Mono
using Content.Shared._CE.ZLevels.Core.EntitySystems; // Mono
using Content.Shared._Mono.Ships; // Mono
using Content.Shared.Administration.Logs;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Containers;
using Robust.Shared.Map; // Mono
using Robust.Shared.Network; // Mono
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed partial class HitscanBasicRaycastSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ISharedAdminLogManager _log = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    // Mono Start
    [Dependency] private SharedShipShieldShellSystem _shields = default!;
    [Dependency] private CESharedZLevelsSystem _levels = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private INetManager _net = default!;
    // Mono End

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanBasicRaycastComponent, HitscanTraceEvent>(OnHitscanFired);
    }

    private void OnHitscanFired(Entity<HitscanBasicRaycastComponent> ent, ref HitscanTraceEvent args)
    {
        var shooter = args.Shooter ?? args.Gun;
        var mapCords = _transform.ToMapCoordinates(args.FromCoordinates);
        var result = Trace(ent.Comp, mapCords, args.ShotDirection, shooter, args.Target, out var shield, out var distance); // Mono

        var trace = new HitscanRaycastFiredEvent
        {
            FromCoordinates = args.FromCoordinates,
            ShotDirection = args.ShotDirection,
            Gun = args.Gun,
            Shooter = args.Shooter,
            HitEntities = [], // Mono
            DistanceTried = distance, // Mono
        };

        if (result?.HitEntity != null) // Mono
        {
            trace.HitEntities.Add(result.Value.HitEntity);

            _log.Add(LogType.HitScanHit,
                $"{ToPrettyString(shooter):user} hit {ToPrettyString(result.Value.HitEntity):target}"
                + $" using {ToPrettyString(args.Gun):entity}.");
        }

        RaiseLocalEvent(ent, ref trace);
        if (!trace.Canceled && shield.IsValid()) // Mono
            PublishShieldHit(ent.Owner, shield, shooter, mapCords.Offset(args.ShotDirection * distance)); // Mono
    }

    // Mono Start
    private RayCastResults? Trace(HitscanBasicRaycastComponent settings, MapCoordinates start, Vector2 direction,
        EntityUid shooter, EntityUid? target, out EntityUid shield, out float distance)
    {
        distance = ShieldRange(start, direction, shooter, settings.MaxDistance, out shield);
        var ray = new CollisionRay(start.Position, direction, (int) settings.CollisionMask);
        var contained = _container.IsEntityOrParentInContainer(shooter);
        var result = _physics.IntersectRay(start.MapId, ray, distance, shooter, false)
            .FirstOrNull(hit => !IsShieldProxy(hit) && (contained || hit.HitEntity == target ||
                CompOrNull<RequireProjectileTargetComponent>(hit.HitEntity)?.Active != true));

        if (result is { } hit)
        {
            shield = EntityUid.Invalid;
            distance = hit.Distance;
        }

        return result;
    }

    public float ShieldRange(MapCoordinates start, Vector2 direction, EntityUid shooter, float range, out EntityUid shield)
    {
        shield = EntityUid.Invalid;
        if (!_maps.TryGetMap(start.MapId, out var map))
            return range;

        var altitude = GetTraceAltitude(shooter, map.Value);
        return _shields.TryFindRayHit(map, altitude, start.Position, direction * range, out shield, out var fraction)
            ? range * fraction
            : range;
    }

    public bool IsShieldProxy(RayCastResults hit)
    {
        if (CompOrNull<ShipShieldColliderComponent>(hit.HitEntity)?.Shield is not { } emitter)
            return false;

        if (emitter != hit.HitEntity)
            return true;

        // A map-mounted emitter also carries the perimeter fixtures. Only ignore its rim.
        if (!TryComp(emitter, out ShipShieldProjectorComponent? shield))
            return false;

        var radii = shield.Shield.Radii;
        var rim = Vector2.Max(new Vector2(radii.X, radii.Y) - new Vector2(0.15f), new Vector2(0.01f));
        var point = Vector2.Transform(hit.HitPos, _transform.GetInvWorldMatrix(emitter));
        return (point / rim).LengthSquared() >= 1f;
    }

    private float GetTraceAltitude(EntityUid shooter, EntityUid map) =>
        _levels.GetAbsoluteAltitude(Transform(shooter).MapUid == map ? shooter : map);

    public void PublishShieldHit(EntityUid shot, EntityUid shield, EntityUid shooter, MapCoordinates point)
    {
        if (!_net.IsServer || !_maps.TryGetMap(point.MapId, out var map))
            return;

        var damage = TryComp(shot, out HitscanBasicDamageComponent? component) ? component.Damage : new DamageSpecifier();
        var impact = new ShipShieldHitscanImpactEvent(point.Position, point.MapId, GetTraceAltitude(shooter, map.Value),
            shot, damage);
        RaiseLocalEvent(shield, ref impact);
    }
    // Mono End
}
