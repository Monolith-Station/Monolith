using Content.Shared._Mono.Weapons.Hitscan.Components;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Hitscan.Systems;
using Robust.Shared.Map;

namespace Content.Shared._Mono.Weapons.Hitscan.Systems;

public sealed class HitscanJumpSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanJumpComponent, HitscanRaycastFiredEvent>(OnHitscanHit, after: [ typeof(HitscanReflectSystem) ]);
    }

    private void OnHitscanHit(Entity<HitscanJumpComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Canceled || args.HitEntity == null || !GetClosestTarget(args.FromCoordinates, ent.Comp.Range, ent.Comp.IgnoredEntities, out var closest))
            return;


    }

    private bool GetClosestTarget(EntityCoordinates coords, float range, out EntityUid? closest)
    {
        var eqe = _lookup.GetEntitiesInRange<MobStateComponent>(coords, range);
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

        return closest.HasValue;
    }

    private bool GetClosestTarget(EntityCoordinates coords, float range, List<EntityUid?> ignoredEnts, out EntityUid? closest)
    {
        var eqe = _lookup.GetEntitiesInRange<MobStateComponent>(coords, range);
        closest = null;

        var cD = range;
        foreach (var ent in eqe)
        {
            if (ignoredEnts.Contains(ent.Owner))
                continue;

            coords.TryDistance(EntityManager, Transform(ent).Coordinates, out var d);

            if (cD > d)
            {
                cD = d;
                closest = ent.Owner;
            }
        }

        return closest.HasValue;
    }
}
