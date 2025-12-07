using Content.Server.Body.Components;
using Content.Shared._Mono.Traits.Physical;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Traits.Physical;

/// <summary>
/// Applies Hemophilia trait effects.
/// </summary>
public sealed class HemophiliaSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly Content.Server.Body.Systems.BloodstreamSystem _bloodstream = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HemophiliaComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HemophiliaComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<HemophiliaComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<HemophiliaComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnStartup(Entity<HemophiliaComponent> ent, ref ComponentStartup args) {}

    private void OnShutdown(Entity<HemophiliaComponent> ent, ref ComponentShutdown args) {}

    private void OnDamageModify(Entity<HemophiliaComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Damage.DamageDict.TryGetValue("Blunt", out var blunt))
            args.Damage.DamageDict["Blunt"] = blunt * ent.Comp.BluntDamageMultiplier;
    }

    private void OnDamageChanged(Entity<HemophiliaComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta is null || !args.DamageIncreased)
            return;

        if (!TryComp<BloodstreamComponent>(ent, out var blood))
            return;

        if (!_prototypes.TryIndex<DamageModifierSetPrototype>(blood.DamageBleedModifiers, out var modifiers))
            return;

        var added = DamageSpecifier.ApplyModifierSet(args.DamageDelta, modifiers);
        if (added.Empty)
            return;

        var extra = added.GetTotal().Float() * ent.Comp.ExtraBleedOnDamageMultiplier;
        if (extra <= 0f)
            return;

        _bloodstream.TryModifyBleedAmount(ent, extra, blood);
    }
}


