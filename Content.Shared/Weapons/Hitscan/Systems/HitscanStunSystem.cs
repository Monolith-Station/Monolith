using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed partial class HitscanStunSystem : EntitySystem
{
    [Dependency] private StaminaSystem _stamina = default!; // Mono - SharedStaminaSystem not ported yet

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanStaminaDamageComponent, HitscanRaycastFiredEvent>(OnHitscanHit, after: [ typeof(HitscanReflectSystem) ]);
    }

    private void OnHitscanHit(Entity<HitscanStaminaDamageComponent> hitscan, ref HitscanRaycastFiredEvent args)
    {
        if (args.Canceled)
            return;

        foreach (var hitEntity in args.HitEntities) // Mono
        {
            _stamina.TakeStaminaDamage(hitEntity, hitscan.Comp.StaminaDamage, source: args.Shooter ?? args.Gun);
        }
    }
}
