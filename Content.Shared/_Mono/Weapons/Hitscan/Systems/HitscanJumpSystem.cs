using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared._Mono.Weapons.Hitscan.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Hitscan.Systems;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Shared._Mono.Weapons.Hitscan.Systems;

public sealed partial class HitscanJumpSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    [Dependency] private EntityQuery<MobThresholdsComponent> _mobQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanJumpComponent, HitscanRaycastFiredEvent>(OnHitscanHit, after: [ typeof(HitscanReflectSystem) ]);
    }

    /// <summary>
    /// When hitscan hits entity, it will instantly fire from it into closest mob entity and so on.
    /// Too many jumps will cause stack overflow error.
    /// Incompatible with HitscanMultiRaycastSystem.
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="args"></param>
    private void OnHitscanHit(Entity<HitscanJumpComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Canceled ||
            args.HitEntities.Count == 0 ||
            args.Shooter == null ||
            !_mobQuery.HasComp(args.HitEntities.First()) ||
            ent.Comp.Count <= 0)
            return;

        ent.Comp.IgnoredEntities.Add(args.Shooter.Value);
        ent.Comp.IgnoredEntities.Add(args.HitEntities.First());
        var fromCoords = Transform(args.HitEntities.First()).Coordinates;

        if (!GetClosestTarget(fromCoords, ent.Comp.Range, ent.Comp.IgnoredEntities, out _, out var delta))
            return;

        ent.Comp.Count -= 1;

        var hitFire = new HitscanTraceEvent
        {
            FromCoordinates = fromCoords,
            ShotDirection = -Vector2.Normalize(delta.Value),
            Gun = args.Gun,
            Shooter = args.HitEntities.First(),
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
