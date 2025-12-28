using Content.Shared.Examine;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Weapons.Melee;

public sealed class MeleeChargeSystem : EntitySystem
{
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WeaponMeleeChargeComponent, ExaminedEvent>(OnExamined);

        SubscribeLocalEvent<WeaponMeleeChargeComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<WeaponMeleeChargeComponent, ItemToggledEvent>(OnToggle);
        SubscribeLocalEvent<WeaponMeleeChargeComponent, ItemToggleActivateAttemptEvent>(OnToggleAttempt);
    }

    private void OnExamined(Entity<WeaponMeleeChargeComponent> ent, ref ExaminedEvent args)
    {
        if (InCooldown(ent))
            args.PushMarkup(Loc.GetString("melee-charge-weakened", ("cooldown", CooldownToSeconds(ent))));
    }

    private void OnMeleeHit(Entity<WeaponMeleeChargeComponent> ent, ref MeleeHitEvent args)
    {
        if (InCooldown(ent))
        {
            args.BonusDamage += ent.Comp.CooldownDamagePenalty;
            return;
        }

        if (!IsActive(ent))
            return;

        TryDeactivate(ent, ent.Comp);
    }

    private void OnToggleAttempt(Entity<WeaponMeleeChargeComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (!InCooldown(ent))
            return;

        _popup.PopupEntity(Loc.GetString("melee-charge-remaining-cooldown", ("remainingCooldown", CooldownToSeconds(ent))),
            args.User ?? ent);

        args.Cancelled = true;
    }

    private void OnToggle(Entity<WeaponMeleeChargeComponent> ent, ref ItemToggledEvent args)
    {
        if (args.Activated)
            Activate(ent, ent.Comp);
        else
            TryDeactivate(ent, ent.Comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveWeaponMeleeChargeComponent, WeaponMeleeChargeComponent>();

        while (query.MoveNext(out var uid, out _, out var charge))
        {
            if (!ActiveTimePassed(charge))
                continue;

            TryDeactivate(uid, charge);
        }
    }

    private void TryDeactivate(EntityUid uid, WeaponMeleeChargeComponent charge)
    {
        if(!_toggle.TryDeactivate(uid))
            return;

        if (HasComp<ActiveWeaponMeleeChargeComponent>(uid))
            RemComp<ActiveWeaponMeleeChargeComponent>(uid);

        charge.CooldownEndTime = TimeSpan.FromSeconds(charge.Cooldown) + _timing.CurTime;
    }

    private void Activate(EntityUid uid, WeaponMeleeChargeComponent charge)
    {
        AddComp<ActiveWeaponMeleeChargeComponent>(uid);
        charge.ActiveEndTime = TimeSpan.FromSeconds(charge.ActiveTime) + _timing.CurTime;
    }

    private bool InCooldown(WeaponMeleeChargeComponent charge)
    {
        return charge.CooldownEndTime > _timing.CurTime;
    }

    private bool IsActive(EntityUid uid)
    {
        return HasComp<ActiveWeaponMeleeChargeComponent>(uid);
    }

    private bool ActiveTimePassed(WeaponMeleeChargeComponent charge)
    {
        return charge.ActiveEndTime < _timing.CurTime;
    }

    private int CooldownToSeconds(Entity<WeaponMeleeChargeComponent> ent)
    {
        return (int) double.Ceiling((ent.Comp.CooldownEndTime - _timing.CurTime).TotalSeconds);
    }
}
