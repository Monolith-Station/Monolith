using Content.Shared._Mono.Blocking;
using Content.Shared.Damage;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Content.Shared.Blocking.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared._Mono.Blocking.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands.Components;
using System.Linq;

namespace Content.Shared.Blocking;

public sealed partial class BlockingSystem : SharedBlockingSystem // Mono
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private void InitializeUser()
    {
        SubscribeLocalEvent<BlockingUserComponent, DamageModifyEvent>(OnUserDamageModified);
        SubscribeLocalEvent<BlockingComponent, DamageModifyEvent>(OnDamageModified);

        SubscribeLocalEvent<BlockingUserComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<BlockingUserComponent, ContainerGettingInsertedAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<BlockingUserComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<BlockingUserComponent, EntityTerminatingEvent>(OnEntityTerminating);

        SubscribeLocalEvent<HandsComponent, ShotAttemptedEvent>(OnBeforeGunShot);
    }

    private void OnParentChanged(EntityUid uid, BlockingUserComponent component, ref EntParentChangedMessage args)
    {
        UserStopBlocking(uid, component);
    }

    private void OnInsertAttempt(EntityUid uid, BlockingUserComponent component, ContainerGettingInsertedAttemptEvent args)
    {
        UserStopBlocking(uid, component);
    }

    private void OnAnchorChanged(EntityUid uid, BlockingUserComponent component, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            return;

        UserStopBlocking(uid, component);
    }

    /// <summary>
    /// Mono: can't shoot with shield
    /// </summary>
    private void OnBeforeGunShot(Entity<HandsComponent> ent, ref ShotAttemptedEvent args)
    {
        if (HasComp<CanShootWithShieldComponent>(args.Used)) // don't bother if this gun will always be allowed to be used
            return;

        var heldItems = _handsSystem.EnumerateHeld(ent, ent.Comp).ToArray();
        foreach (var item in heldItems)
        {
            if (HasComp<BlockingComponent>(item))
            {
                _popupSystem.PopupClient(Loc.GetString("shield-user-attempt-shoot"), ent);
                args.Cancel();
                break;
            }
        }
    }

    private void OnUserDamageModified(EntityUid uid, BlockingUserComponent component, DamageModifyEvent args)
    {
        if (TryComp<BlockingComponent>(component.BlockingItem, out var blocking)) // Mono
        {
            if (args.Damage.GetTotal() <= 0)
                return;

            // A shield should only block damage it can itself absorb. To determine that we need the Damageable component on it.
            if (!TryComp<DamageableComponent>(component.BlockingItem, out var dmgComp))
                return;

            if (TryComp<ItemToggleComponent>(component.BlockingItem, out var toggleComponent) && !toggleComponent.Activated) // Mono
                return;

            var blockFraction = blocking.IsBlocking ? blocking.ActiveBlockFraction : blocking.PassiveBlockFraction;
            blockFraction = Math.Clamp(blockFraction, 0, 1);
            _damageable.TryChangeDamage(component.BlockingItem,
                blockFraction * args.OriginalDamage,
                armorPenetration: args.ArmorPenetration); // Goob edit

            var modify = new DamageModifierSet();
            foreach (var key in dmgComp.Damage.DamageDict.Keys)
            {
                modify.Coefficients.TryAdd(key, 1 - blockFraction);
            }

            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage,
                DamageSpecifier.PenetrateArmor(modify ,args.ArmorPenetration)); // Goob edit

            if (blocking.IsBlocking && !args.Damage.Equals(args.OriginalDamage))
            {
                _audio.PlayPvs(blocking.BlockSound, uid);
            }
        }
    }

    private void OnDamageModified(EntityUid uid, BlockingComponent component, DamageModifyEvent args)
    {
        var modifier = component.IsBlocking ? component.ActiveBlockDamageModifier : component.PassiveBlockDamageModifer;
        if (modifier == null)
        {
            return;
        }

        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifier);
    }

    private void OnEntityTerminating(EntityUid uid, BlockingUserComponent component, ref EntityTerminatingEvent args)
    {
        if (!TryComp<BlockingComponent>(component.BlockingItem, out var blockingComponent))
            return;

        StopBlockingHelper(component.BlockingItem.Value, blockingComponent, uid);

    }

    /// <summary>
    /// Check for the shield and has the user stop blocking
    /// Used where you'd like the user to stop blocking, but also don't want to remove the <see cref="BlockingUserComponent"/>
    /// </summary>
    /// <param name="uid">The user blocking</param>
    /// <param name="component">The <see cref="BlockingUserComponent"/></param>
    private void UserStopBlocking(EntityUid uid, BlockingUserComponent component)
    {
        if (TryComp<BlockingComponent>(component.BlockingItem, out var blockComp) && blockComp.IsBlocking)
            StopBlocking(component.BlockingItem.Value, blockComp, uid);
    }
}
