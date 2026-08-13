using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Content.Shared._Mono.Weapons.Hitscan.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Hitscan.Systems;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Shared._Mono.Weapons.Hitscan.Systems;

public sealed class HitscanJumpSystem : EntitySystem
{
    [Robust.Shared.IoC.Dependency] private EntityLookupSystem _lookup = default!;
    [Robust.Shared.IoC.Dependency] private SharedTransformSystem _transform = default!;

    [Robust.Shared.IoC.Dependency] private EntityQuery<MobThresholdsComponent> _mobQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanJumpComponent, HitscanRaycastFiredEvent>(OnHitscanHit, after: [ typeof(HitscanReflectSystem) ]);
    }

    private void OnHitscanHit(Entity<HitscanJumpComponent> ent, ref HitscanRaycastFiredEvent args) // Important note - incompatible with HitscanMultiRaycastSystem.
    {
        if (args.Canceled ||
            args.HitEntity.Count == 0 ||
            args.Shooter == null ||
            _mobQuery.HasComp(args.HitEntity.First()) ||
            ent.Comp.Count <= 0)
            return;

        ent.Comp.IgnoredEntities.Add(args.Shooter.Value);
        ent.Comp.IgnoredEntities.Add(args.HitEntity.First());
        var fromCoords = Transform(args.HitEntity.First()).Coordinates;

        if (!GetClosestTarget(fromCoords, ent.Comp.Range, ent.Comp.IgnoredEntities, out _, out var delta))
            return;

        ent.Comp.Count -= 1;

        var hitFire = new HitscanTraceEvent
        {
            FromCoordinates = fromCoords,
            ShotDirection = -Vector2.Normalize(delta.Value),
            Gun = args.Gun,
            Shooter = args.HitEntity.First(),
        };

        RaiseLocalEvent(ent, ref hitFire);
    }

    private bool GetClosestTarget(EntityCoordinates coords,
        float range,
        [NotNullWhen(true)] out EntityUid? closest,
        [NotNullWhen(true)] out Vector2? delta)
    {
        return GetClosestTarget(coords, range, [], out closest, out delta);
    }

    private bool GetClosestTarget(EntityCoordinates coords,
        float range,
        HashSet<EntityUid> ignoredEnts,
        [NotNullWhen(true)] out EntityUid? closest,
        [NotNullWhen(true)] out Vector2? delta)
    {
        var eqe = _lookup.GetEntitiesInRange<MobStateComponent>(coords, range);

        delta = null;
        closest = null;

        var cD = range;
        foreach (var ent in eqe)
        {
            if (ignoredEnts.TryFirstOrNull(e => e.Id == ent.Owner.Id, out _))
                continue;

            coords.TryDistance(EntityManager, Transform(ent).Coordinates, out var d);

            if (cD > d)
            {
                cD = d;
                closest = ent.Owner;
            }
        }

        if (closest.HasValue)
            delta = _transform.ToWorldPosition(coords) - _transform.ToWorldPosition(Transform(closest.Value).Coordinates);

        return closest.HasValue;
    }
}
