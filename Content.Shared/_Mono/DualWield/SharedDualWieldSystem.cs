using Content.Shared.Examine;
using Content.Shared.Flash;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;

namespace Content.Shared._Mono.DualWield;

/// <summary>
/// Handles entering and leaving the dual-wield stance.
///
/// A user enters the stance by activating a LightWeaponComponent item in hand while at least one other hand also holds a light weapon.
/// Activating again, switching hands, or either hand stopping to hold a light weapon leaves the stance.
/// </summary>
public sealed partial class SharedDualWieldSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Run before everything else that consumes UseInHandEvent on weapons we mark light
        // racking a pistol slide, spinning a revolver cylinder, toggling an e-sword, cycling disabler fire modes.
        // Dual-wielding takes priority; if we leave the event unhandled their behaviour still runs.
        SubscribeLocalEvent<LightWeaponComponent, UseInHandEvent>(OnUseInHand, before:
        [
            typeof(SharedWieldableSystem),
            typeof(SharedGunSystem),
            typeof(BatteryWeaponFireModesSystem),
            typeof(ItemToggleSystem),
            typeof(SharedFlashSystem),
        ]);

        // Dual-wielding takes priority over wielding too. TryWield raises this on the item itself, so
        // this covers every route to wielding rather than just the use-in-hand one we run before.
        SubscribeLocalEvent<LightWeaponComponent, WieldAttemptEvent>(OnWieldAttempt);

        SubscribeLocalEvent<LightWeaponComponent, ExaminedEvent>(OnExamined);

        // One of the two weapons is always in the active hand, so swapping hands always fires this.
        SubscribeLocalEvent<LightWeaponComponent, HandDeselectedEvent>(OnHandDeselected);

        SubscribeLocalEvent<DualWieldComponent, DidEquipHandEvent>(OnDidEquipHand);
        SubscribeLocalEvent<DualWieldComponent, DidUnequipHandEvent>(OnDidUnequipHand);
    }

    #region Public API

    public bool IsDualWielding(Entity<DualWieldComponent?> user)
    {
        return Resolve(user, ref user.Comp, false);
    }

    /// <summary>
    /// Gets the weapon held in one of the two dual-wielded hands.
    /// </summary>
    /// <param name="left">True for the first-indexed hand, false for the second.</param>
    public bool TryGetDualWeapon(Entity<DualWieldComponent?> user, bool left, out EntityUid weapon)
    {
        weapon = default;

        if (!Resolve(user, ref user.Comp, false))
            return false;

        var handId = left ? user.Comp.LeftHandId : user.Comp.RightHandId;

        if (!_hands.TryGetHand(user.Owner, handId, out var hand) || hand.HeldEntity is not { } held)
            return false;

        weapon = held;
        return true;
    }

    /// <summary>
    /// Whether <paramref name="weapon"/> is one of the two weapons the user is currently dual-wielding.
    /// </summary>
    /// <param name="left">Set to true if it is the first-indexed hand's weapon.</param>
    public bool IsDualWieldWeapon(Entity<DualWieldComponent?> user, EntityUid weapon, out bool left)
    {
        left = false;

        if (!Resolve(user, ref user.Comp, false))
            return false;

        if (TryGetDualWeapon(user, true, out var leftWeapon) && leftWeapon == weapon)
        {
            left = true;
            return true;
        }

        return TryGetDualWeapon(user, false, out var rightWeapon) && rightWeapon == weapon;
    }

    /// <summary>
    /// Whether <paramref name="weapon"/> could start a dual-wield stance right now.
    /// </summary>
    public bool CanDualWield(EntityUid user, EntityUid weapon)
    {
        return CanDualWield(user, weapon, out _, out _);
    }

    public void EndStance(Entity<DualWieldComponent?> user)
    {
        if (!Resolve(user, ref user.Comp, false))
            return;

        RemComp<DualWieldComponent>(user);
    }

    #endregion

    private void OnUseInHand(Entity<LightWeaponComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        // Already in the stance with this weapon - leave it.
        if (TryComp<DualWieldComponent>(args.User, out var dualWield)
            && IsDualWieldWeapon((args.User, dualWield), ent, out _))
        {
            EndStance((args.User, dualWield));
            args.Handled = true;
            return;
        }

        if (!CanDualWield(args.User, ent.Owner, out var usedHandId, out var otherHandId))
            return;

        // Index order decides which is "left"; see DualWieldComponent.
        var first = otherHandId;
        var second = usedHandId;

        foreach (var hand in _hands.EnumerateSortedHands(args.User))
        {
            if (hand != usedHandId && hand != otherHandId)
                continue;

            first = hand;
            second = hand == usedHandId ? otherHandId : usedHandId;
            break;
        }

        var comp = EnsureComp<DualWieldComponent>(args.User);
        comp.LeftHandId = first;
        comp.RightHandId = second;
        Dirty(args.User, comp);

        args.Handled = true;
    }

    private bool CanDualWield(EntityUid user, EntityUid weapon, out string usedHandId, out string otherHandId)
    {
        usedHandId = string.Empty;
        otherHandId = string.Empty;

        // Cyborg hands are atypical, they never dual wield.
        if (HasComp<BorgChassisComponent>(user))
            return false;

        if (!TryComp<LightWeaponComponent>(weapon, out var light) || !IsEligible((weapon, light)))
            return false;

        if (!_hands.IsHolding(user, weapon, out var handInUse))
            return false;

        // "At least one other hand holds a light weapon" - not strictly "the" other hand, so that
        // species with more than two arms work.
        if (!TryGetPartnerHand(user, handInUse.Name, out otherHandId))
            return false;

        usedHandId = handInUse.Name;
        return true;
    }

    private bool TryGetPartnerHand(EntityUid user, string handId, out string otherHandId)
    {
        otherHandId = string.Empty;

        foreach (var name in _hands.EnumerateSortedHands(user))
        {
            if (name == handId)
                continue;

            if (!_hands.TryGetHand(user, name, out var hand) || hand.HeldEntity is not { } held)
                continue;

            if (!TryComp<LightWeaponComponent>(held, out var light) || !IsEligible((held, light)))
                continue;

            otherHandId = name;
            return true;
        }

        return false;
    }

    /// <summary>
    /// A light weapon can still be disqualified.
    /// </summary>
    /// <remarks>
    /// Being merely <see cref="WieldableComponent"/> does not disqualify it
    /// wielding is optional there, and dual-wielding takes priority over it OnWieldAttempt. An item that is <i>currently</i> wielded does
    /// since it is already occupying the other hand with a virtual item.
    /// Requiring a wield to attack at all does too: dual-wielding one would produce a stance that cannot swing or fire.
    /// </remarks>
    private bool IsEligible(Entity<LightWeaponComponent> ent)
    {
        if (HasComp<VirtualItemComponent>(ent))
            return false;

        if (RequiresWield(ent))
            return false;

        return !TryComp<WieldableComponent>(ent, out var wieldable) || !wieldable.Wielded;
    }

    /// <summary>
    /// Whether the weapon can never be dual-wielded, as opposed to just not right now. A weapon that cannot attack unwielded is useless in the stance, so it is excluded permanently.
    /// </summary>
    private bool RequiresWield(EntityUid uid)
    {
        return HasComp<MeleeRequiresWieldComponent>(uid) || HasComp<GunRequiresWieldComponent>(uid);
    }

    /// <summary>
    /// Light weapons are otherwise indistinguishable from any other small weapon, so say so on examine. Skipped for weapons that carry the marker by inheritance but can never actually dual-wield.
    /// </summary>
    private void OnExamined(Entity<LightWeaponComponent> ent, ref ExaminedEvent args)
    {
        if (RequiresWield(ent))
            return;

        args.PushMarkup(Loc.GetString("dual-wield-examine"));
    }

    /// <summary>
    /// Dual-wielding takes priority over wielding. If the weapon could be dual-wielded instead, or the user is already in the stance - refuse the wield rather than let it silently win.
    /// </summary>
    private void OnWieldAttempt(Entity<LightWeaponComponent> ent, ref WieldAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!IsDualWielding(args.User) && !CanDualWield(args.User, ent.Owner))
            return;

        _popup.PopupClient(Loc.GetString("dual-wield-cannot-wield", ("item", ent.Owner)), args.User, args.User);
        args.Cancel();
    }

    private void OnHandDeselected(Entity<LightWeaponComponent> ent, ref HandDeselectedEvent args)
    {
        EndStance(args.User);
    }

    private void OnDidEquipHand(Entity<DualWieldComponent> ent, ref DidEquipHandEvent args)
    {
        ValidateStance(ent);
    }

    private void OnDidUnequipHand(Entity<DualWieldComponent> ent, ref DidUnequipHandEvent args)
    {
        ValidateStance(ent);
    }

    /// <summary>
    /// Leaves the stance unless both recorded hands still hold an eligible light weapon.
    /// </summary>
    private void ValidateStance(Entity<DualWieldComponent> ent)
    {
        if (StillHolding(ent, ent.Comp.LeftHandId) && StillHolding(ent, ent.Comp.RightHandId))
            return;

        EndStance(ent.Owner);
    }

    private bool StillHolding(EntityUid user, string handId)
    {
        if (!_hands.TryGetHand(user, handId, out var hand) || hand.HeldEntity is not { } held)
            return false;

        return TryComp<LightWeaponComponent>(held, out var light) && IsEligible((held, light));
    }
}
