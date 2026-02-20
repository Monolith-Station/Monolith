using System.Linq;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._Mono.Claws;
public abstract partial class SharedClawsSystem
{
    public void UpdateDeclaw(EntityUid uid, DeclawedComponent comp, float updateTime)
    {
        var hands = _hands.EnumerateHands(uid).ToArray();
        if (!_hands.EnumerateHeld(uid).Any())
        {
            comp.ItemHoldingTime = TimeSpan.Zero;
            _effects.TryRemoveStatusEffect(uid, "Jitter");
            return;
        }

        if (!_state.IsAlive(uid))
            return;

        comp.ItemHoldingTime += TimeSpan.FromSeconds(updateTime);

        if (comp.ItemHoldingTime.Seconds >= comp.MaxItemHoldingTime.Seconds / 2)
        {
            _jitter.DoJitter(uid,
                    TimeSpan.FromSeconds(updateTime),
                    true,
                    1,
                    0.5f * (comp.ItemHoldingTime.Seconds - comp.MaxItemHoldingTime.Seconds / 2));
        }

        if (comp.ItemHoldingTime.Seconds < comp.MaxItemHoldingTime.Seconds)
            return;

        foreach (var hand in hands)
        {
            DeclawDrop(uid, hand, hand.HeldEntity);
        }

        comp.ItemHoldingTime = TimeSpan.Zero;

        Dirty(uid,comp);
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
        Dirty(uid, declaw);
    }

    private void DeclawDrop(EntityUid uid, Hand hand, EntityUid? item)
    {
        _hands.SetActiveHand(uid, hand);
        if (item == null)
            return;

        _hands.TryDrop(uid);
        _throw.TryThrow(item.Value, _random.NextVector2(), 1, uid);
        _popup.PopupEntity(Loc.GetString("declaw-item-drop"), uid, PopupType.MediumCaution);
    }

    protected void DeclawDrop(EntityUid uid, EntityUid item)
    {
        _hands.TryDrop(uid);
        _throw.TryThrow(item, _random.NextVector2(), 1, uid);
        _popup.PopupEntity(Loc.GetString("declaw-item-drop"), uid, PopupType.MediumCaution);
    }
}
