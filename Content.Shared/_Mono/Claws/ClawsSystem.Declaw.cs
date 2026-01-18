using System.Linq;
using Content.Shared.Hands;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._Mono.Claws;
public abstract partial class SharedClawsSystem
{
    private readonly float _updateCooldown = 1f;
    private TimeSpan _updateTimer = TimeSpan.Zero;
    private void InitializeDeclaw()
    {
        SubscribeLocalEvent<DeclawedComponent, MeleeAttackEvent>(OnAttack);
        SubscribeLocalEvent<DeclawedComponent, UnequippedHandEvent>(OnUnequip);
    }

    public override void Update(float frameTime)
    {
        if (_updateTimer < TimeSpan.FromSeconds(_updateCooldown))
        {
            _updateTimer += TimeSpan.FromSeconds(frameTime);
            return;
        }

        var ents = EntityQueryEnumerator<DeclawedComponent>();

        while (ents.MoveNext(out var uid, out var comp))
        {
            var itemEnum = _hands.EnumerateHeld(uid).ToArray();
            if (itemEnum.Length == 0)
                continue;

            if (!_state.IsAlive(uid))
                continue;

            comp.ItemHoldingTime += TimeSpan.FromSeconds(_updateCooldown);

            if (comp.ItemHoldingTime.Seconds >= comp.MaxItemHoldingTime.Seconds / 2)
            {
                _jitter.DoJitter(uid,
                    TimeSpan.FromSeconds(_updateCooldown),
                    true,
                    3,
                    0.1f * comp.ItemHoldingTime.Seconds - comp.MaxItemHoldingTime.Seconds / 2);
            }

            if (comp.ItemHoldingTime.Seconds < comp.MaxItemHoldingTime.Seconds)
                continue;

            foreach (var item in itemEnum)
            {
                DeclawDrop(uid, item);
            }

            comp.ItemHoldingTime = TimeSpan.Zero;
        }

        _updateTimer = TimeSpan.Zero;

    }

    private void OnAttack(Entity<DeclawedComponent> ent, ref MeleeAttackEvent args)
    {
        if (ent.Owner == args.Weapon)
            return;

        var r = _random.NextFloat();

        if (!(r < ent.Comp.DropChanceOnMelee))
            return;

        DeclawDrop(ent, args.Weapon);
    }

    private void OnUnequip(EntityUid uid, DeclawedComponent comp, UnequippedHandEvent args)
    {
        if (_hands.EnumerateHeld(uid).Any())
            return;

        if (!_state.IsAlive(uid))
            return;

        comp.ItemHoldingTime = TimeSpan.Zero;
        _effects.TryRemoveStatusEffect(uid, "Jitter");

        args.Handled = true;
    }

    public void Declaw(EntityUid uid, ClawsComponent claws)
    {
        claws.ClawStage = 0;
        claws.GrowTimer = TimeSpan.Zero;

        var declaw = EnsureComp<DeclawedComponent>(uid);
        declaw.RawMeleeDamage = claws.DeclawedMeleeDamage;

        _popup.PopupEntity(Loc.GetString("declaw-success"), uid, PopupType.LargeCaution);
        _damage.TryChangeDamage(uid, claws.DamageOnDeclaw, true);

        UpdateClaws(uid, claws);
        Dirty(uid, claws);
    }

    private void DeclawDrop(EntityUid uid, EntityUid item)
    {
        _hands.TryDrop(uid);
        _throw.TryThrow(item, _random.NextVector2(), 1, uid);
        _popup.PopupEntity(Loc.GetString("declaw-item-drop"), uid, PopupType.MediumCaution);
    }
}
