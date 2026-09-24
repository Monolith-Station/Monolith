using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Random;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed partial class HitscanReflectSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!; // Mono
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanReflectComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
    }

    private void OnHitscanHit(Entity<HitscanReflectComponent> hitscan, ref HitscanRaycastFiredEvent args)
    {
        if (hitscan.Comp.ReflectiveType == ReflectType.None || args.HitEntities.Count == 0) // Mono
            return;

        if (hitscan.Comp.CurrentReflections >= hitscan.Comp.MaxReflections)
            return;

        // Mono begin
        DamageSpecifier damage = new();
        if (EntityManager.TryGetComponent<HitscanBasicDamageComponent>(hitscan, out var hitscanDamage))
            damage = hitscanDamage.Damage * _damage.UniversalHitscanDamageModifier;

        // Mono - Use hitscan damage component if available
        var ev = new HitScanReflectAttemptEvent(args.Shooter ?? args.Gun, args.Gun, hitscan.Comp.ReflectiveType, args.ShotDirection, false, damage);
        // Mono End
        RaiseLocalEvent(args.HitEntities.First(), ref ev); // Mono

        if (!ev.Reflected)
            return;

        hitscan.Comp.CurrentReflections++;

        args.Canceled = true;

        var fromEffect = Transform(args.HitEntities.First()).Coordinates; // Mono

        var hitFiredEvent = new HitscanTraceEvent
        {
            FromCoordinates = fromEffect,
            ShotDirection = ev.Direction,
            Gun = args.Gun,
            Shooter = args.HitEntities.First(), // Mono
        };

        RaiseLocalEvent(hitscan, ref hitFiredEvent);
    }
}
