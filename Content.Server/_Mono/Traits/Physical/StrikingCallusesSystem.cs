using Content.Shared._Mono.Traits.Physical;
using Content.Shared.Damage;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._Mono.Traits.Physical;

/// <summary>
/// Applies the Striking Calluses bonus to unarmed melee damage.
/// </summary>
public sealed class StrikingCallusesSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StrikingCallusesComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
    }

    private void OnGetMeleeDamage(Entity<StrikingCallusesComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (args.User != args.Weapon)
            return;

        if (ent.Comp.BluntBonus <= 0)
            return;

        var bonus = new DamageSpecifier();
        bonus.DamageDict.Add("Blunt", Shared.FixedPoint.FixedPoint2.New(ent.Comp.BluntBonus));
        args.Damage += bonus;
    }
}
