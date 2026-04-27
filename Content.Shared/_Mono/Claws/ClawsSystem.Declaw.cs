using System.Linq;
using Content.Shared._Mono.Claws.ClawTypes;
using Content.Shared._Mono.Claws.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._Mono.Claws;

public abstract partial class SharedClawsSystem
{
    public void UpdateDeclaw(EntityUid uid, Declawed declawed, ClawsComponent claws, float updateTime)
    {
        if (!_state.IsAlive(uid))
            return;

        var hands = _hands.EnumerateHands(uid).ToArray();
        if (!_hands.EnumerateHeld(uid).Any())
        {
            claws.DeclawItemHoldTimer = TimeSpan.Zero;
            _effects.TryRemoveStatusEffect(uid, "Jitter");
            return;
        }

        claws.DeclawItemHoldTimer += TimeSpan.FromSeconds(updateTime);

        if (claws.DeclawItemHoldTimer.Seconds >= declawed.MaxItemHoldingTime.Seconds / 2)
        {
            _jitter.DoJitter(uid,
                    TimeSpan.FromSeconds(updateTime),
                    true,
                    1,
                    0.5f * (claws.DeclawItemHoldTimer.Seconds - declawed.MaxItemHoldingTime.Seconds / 2));
        }

        if (claws.DeclawItemHoldTimer.Seconds < declawed.MaxItemHoldingTime.Seconds)
            return;

        foreach (var hand in hands)
        {
            DeclawDrop(uid, hand, hand.HeldEntity);
        }

        claws.DeclawItemHoldTimer = TimeSpan.Zero;

        Dirty(uid, claws);
    }

    public void Declaw(EntityUid uid, ClawsComponent claws)
    {
        if (!claws.Claws.TryGetValue(-1, out var declawedProto))
            return;

        claws.ClawStage = declawedProto;
        claws.GrowTimer = TimeSpan.Zero;

        if (!TryGetStage<Declawed>(claws, out var declawedStage))
            return;

        _popup.PopupEntity(Loc.GetString("declaw-success"), uid, PopupType.LargeCaution);
        _damage.TryChangeDamage(uid, declawedStage.DamageOnDeclaw, true);

        UpdateClaws(uid, claws);
        Dirty(uid, claws);
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
