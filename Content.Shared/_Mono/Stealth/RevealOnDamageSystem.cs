using Content.Shared._Mono.Stealth.Components;
using Content.Shared.Damage;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;

namespace Content.Shared._Mono.Stealth;

public sealed partial class RevealOnDamageSystem : EntitySystem
{
    [Dependency] private SharedStealthSystem _stealth = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevealOnDamageComponent, DamageChangedEvent>(OnDamageTaken);
    }

    private void OnDamageTaken(Entity<RevealOnDamageComponent> ent, ref DamageChangedEvent args)
    {
        if (!TryComp<StealthComponent>(ent, out var stealthComp)) // what are we even doing
            return;
        if (!args.DamageIncreased) // don't reveal on healing
            return;
        if (args.DamageDelta == null)
            return;
        foreach (var type in ent.Comp.DamageTypes)
        {
            if (!args.DamageDelta.DamageDict.TryGetValue(type, out var damage))
                continue;
            if (damage > ent.Comp.DamageThreshold)
            {
                _stealth.ModifyVisibility(ent, ent.Comp.VisibilityOnDamage, stealthComp);
                break; // no point in any more checking.
            }
        }
    }
}