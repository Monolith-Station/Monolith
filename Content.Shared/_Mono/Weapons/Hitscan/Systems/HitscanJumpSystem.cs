using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._Mono.Weapons.Hitscan.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Hitscan.Systems;
using Robust.Shared.Map;

namespace Content.Shared._Mono.Weapons.Hitscan.Systems;

public sealed class HitscanJumpSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanJumpComponent, HitscanRaycastFiredEvent>(OnHitscanHit, after: [ typeof(HitscanReflectSystem) ]);
    }

    private void OnHitscanHit(Entity<HitscanJumpComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Canceled ||
            args.HitEntity == null ||
            args.Shooter == null ||
            !HasComp<MobThresholdsComponent>(args.HitEntity) ||
            ent.Comp.Count <= 0)
            return;

        ent.Comp.IgnoredEntities.Add(args.Shooter.Value.Id);
        ent.Comp.IgnoredEntities.Add(args.HitEntity.Value.Id);
        var fromCoords = Transform(args.HitEntity.Value).Coordinates;

        if (!GetClosestTarget(fromCoords, ent.Comp.Range, ent.Comp.IgnoredEntities, out _, out var delta))
            return;

        ent.Comp.Count -= 1;

        var hitFire = new HitscanTraceEvent
        {
            FromCoordinates = fromCoords,
            ShotDirection = -Vector2.Normalize(delta.Value),
            Gun = args.Gun,
            Shooter = args.HitEntity,
        };

        RaiseLocalEvent(ent, ref hitFire);
    }

    private bool GetClosestTarget(EntityCoordinates coords,
        float range,
        [NotNullWhen(true)] out EntityUid? closest,
        [NotNullWhen(true)] out Vector2? delta)
    {
        var eqe = _lookup.GetEntitiesInRange<MobStateComponent>(coords, range);
        delta = null;
        closest = null;

        var cD = range;
        foreach (var ent in eqe)
        {
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

    private bool GetClosestTarget(EntityCoordinates coords,
        float range,
        HashSet<int> ignoredEnts,
        [NotNullWhen(true)] out EntityUid? closest,
        [NotNullWhen(true)] out Vector2? delta)
    {
        var eqe = _lookup.GetEntitiesInRange<MobStateComponent>(coords, range);

        delta = null;
        closest = null;

        var cD = range;
        foreach (var ent in eqe)
        {
            if (ignoredEnts.Contains(ent.Owner.Id))
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
