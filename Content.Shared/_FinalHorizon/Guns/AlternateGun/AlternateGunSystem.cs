using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Shared._FinalHorizon.Guns.AlternateGun;

public sealed partial class AlternateGunSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedGunSystem _gunSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handSystem = default!;
    [Dependency] private readonly SharedAudioSystem _sound = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AlternateGunAmmoProviderComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<AlternateGunComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<AlternateGunComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(EntityUid uid, AlternateGunComponent comp, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        var user = args.User;

        if (comp.Enabled)
            args.Verbs.Add(new Verb()
            {
                Text = Loc.GetString(comp.DisableMessage),
                Act = () =>
                {
                    _sound.PlayPredicted(comp.SwitchSound, uid, user);
                    comp.Enabled = false;
                },
            });
        else
            args.Verbs.Add(new Verb()
            {
                Text = Loc.GetString(comp.EnableMessage),
                Act = () =>
                {
                    _sound.PlayPredicted(comp.SwitchSound, uid, user);
                    comp.Enabled = true;
                },
            });
    }

    private void OnExamine(EntityUid uid, AlternateGunComponent comp, ref ExaminedEvent args)
    {
        var msg = Loc.GetString(comp.DisabledMessage);
        if (comp.Enabled)
            msg = Loc.GetString(comp.EnabledMessage);
        args.PushMessage(FormattedMessage.FromMarkupOrThrow(msg), 5);
    }

    private void OnTakeAmmo(EntityUid uid, AlternateGunAmmoProviderComponent comp, ref TakeAmmoEvent args)
    {

        if (args.User == null)
            return;
        var held = _handSystem.GetActiveItem(args.User.Value);
        if (held == null || !TryComp<ItemSlotsComponent>(held, out var slots) || slots == null)
            return;

        var ammoEnt = _itemSlots.GetItemOrNull(held.Value, comp.Slot);

        if (ammoEnt != null)
            args.Ammo.Add((ammoEnt, _gunSystem.EnsureShootable(ammoEnt.Value)));

    }

    public Entity<GunComponent> CheckForAltGun(Entity<GunComponent> gun)
    {
        if (!TryComp<AlternateGunComponent>(gun, out var comp) || !comp.Enabled)
            return gun;

        var ent = _itemSlots.GetItemOrNull(gun, comp.Slot);

        if (ent != null && TryComp<GunComponent>(ent, out var altGunComp))
        {
            return (ent.Value, altGunComp);
        }
        return gun;
    }
}
