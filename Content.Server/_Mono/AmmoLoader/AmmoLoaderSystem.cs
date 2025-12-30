using System.Linq;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Mono.AmmoLoader;
using Content.Server._Mono.SpaceArtillery.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Server._Mono.AmmoLoader;

public sealed class AmmoLoaderSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly DeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AmmoLoaderComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<AmmoLoaderComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<AmmoLoaderComponent, GetVerbsEvent<AlternativeVerb>>(AddFlushVerb);
        SubscribeLocalEvent<AmmoLoaderComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<AmmoLoaderComponent, LinkAttemptEvent>(OnLinkAttempt);
    }

    private void OnLinkAttempt(Entity<AmmoLoaderComponent> ent, ref LinkAttemptEvent args)
    {
        if (args.Source != ent.Owner)
            return;

        if (TryComp<DeviceLinkSourceComponent>(ent, out var sourceComponent) &&
            sourceComponent.LinkedPorts.Count > ent.Comp.MaxConnections)
        {
            args.Cancel();
        }
    }

    private void OnComponentInit(Entity<AmmoLoaderComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Container = _containers.EnsureContainer<Container>(ent, AmmoLoaderComponent.ContainerId);

        _deviceLink.EnsureSourcePorts(ent, ent.Comp.LoadPort);
    }

    private void OnInteractHand(Entity<AmmoLoaderComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Container.ContainedEntities.Count > 0)
        {
            TryEjectContents(ent, ent.Comp);
            args.Handled = true;
        }
    }

    private void AddFlushVerb(Entity<AmmoLoaderComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (ent.Comp.Container.ContainedEntities.Count == 0)
            return;

        var user = args.User;

        var ejectableCount = 0;

        foreach (var contained in ent.Comp.Container.ContainedEntities)
        {
            if (_containers.CanRemove(contained, ent.Comp.Container))
                ejectableCount++;
        }

        if (ejectableCount > 0)
        {
            AlternativeVerb ejectVerb = new()
            {
                Act = () => TryEjectContents(ent, ent.Comp),
                Category = VerbCategory.Eject,
                Text = Loc.GetString("ammo-loader-eject-verb"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
                Priority = 0,
            };
            args.Verbs.Add(ejectVerb);
        }

        var linkedArtillery = GetLinkedArtillery(ent);

        foreach (var artillery in linkedArtillery)
        {
            var artilleryName = MetaData(artillery).EntityName;
            var artilleryId = artillery.ToString();

            var (ammoCount, ammoCapacity) = (0, 0);
            if (_gun.TryGetGun(artillery, out var gunUid, out _))
            {
                if (TryComp<MagazineAmmoProviderComponent>(gunUid, out _))
                {
                    var ev = new GetAmmoCountEvent();
                    RaiseLocalEvent(gunUid, ref ev, false);
                    ammoCount = ev.Count;
                    ammoCapacity = ev.Capacity;
                }
                else if (TryComp<BallisticAmmoProviderComponent>(gunUid, out var ammoProvider))
                {
                    ammoCount = ammoProvider.Count;
                    ammoCapacity = ammoProvider.Capacity;
                }
            }

            AlternativeVerb flushVerb = new()
            {
                Act = () => TryFlushToArtillery(ent, ent.Comp, artillery, user),
                Text = Loc.GetString("ammo-loader-flush-to-artillery-with-ammo-and-id",
                    ("artillery", artilleryName),
                    ("ammo", ammoCount),
                    ("capacity", ammoCapacity),
                    ("id", artilleryId)),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
                Priority = 1,
            };
            args.Verbs.Add(flushVerb);
        }
    }

    private void OnAfterInteractUsing(Entity<AmmoLoaderComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (ent.Comp.Container.ContainedEntities.Count >= ent.Comp.MaxCapacity)
        {
            _popup.PopupEntity(Loc.GetString("ammo-loader-insert-fail"), ent, args.User);
            args.Handled = true;
            return;
        }

        if (!CanInsert(ent, ent.Comp, args.Used))
            return;

        if (_containers.Insert(args.Used, ent.Comp.Container))
        {
            _popup.PopupEntity(Loc.GetString("ammo-loader-insert-success"), ent, args.User);
            args.Handled = true;
        }
    }

    private bool CanInsert(Entity<AmmoLoaderComponent> ent, AmmoLoaderComponent component, EntityUid entity)
    {
        if (!Transform(ent).Anchored)
            return false;

        if (!_containers.CanInsert(entity, component.Container))
            return false;

        if (!HasComp<BallisticAmmoProviderComponent>(entity) &&
            !HasComp<AmmoComponent>(entity) &&
            !HasComp<CartridgeAmmoComponent>(entity))
            return false;

        return true;
    }

    private void TryEjectContents(Entity<AmmoLoaderComponent> ent, AmmoLoaderComponent component)
    {
        foreach (var entity in component.Container.ContainedEntities.ToArray())
        {
            _containers.Remove(entity, component.Container);
        }
    }

    private bool ValidateFlush(Entity<AmmoLoaderComponent> ent, AmmoLoaderComponent component, EntityUid user)
    {
        if (!Transform(ent).Anchored)
        {
            _popup.PopupEntity(Loc.GetString("ammo-loader-not-anchored"), ent, user);
            return false;
        }

        if (component.Container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("ammo-loader-empty"), ent, user);
            return false;
        }

        return true;
    }

    private void TryFlushToArtillery(Entity<AmmoLoaderComponent> ent, AmmoLoaderComponent component, EntityUid artillery, EntityUid user)
    {
        if (!ValidateFlush(ent, component, user))
            return;

        component.Engaged = true;
        Dirty(ent, component);

        var artilleryName = MetaData(artillery).EntityName;
        if (TryTransferAmmoTo(ent, artillery))
        {
            _popup.PopupEntity(Loc.GetString("ammo-loader-flushed-to-artillery", ("artillery", artilleryName)), ent, user);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("ammo-loader-transfer-failed-to-artillery", ("artillery", artilleryName)), ent, user);
            component.Engaged = false;
            Dirty(ent, component);
        }
    }

    private List<EntityUid> GetLinkedArtillery(Entity<AmmoLoaderComponent> loader)
    {
        var linkedArtillery = new List<EntityUid>();

        if (!TryComp<DeviceLinkSourceComponent>(loader, out var sourceComponent))
            return linkedArtillery;

        foreach (var (linkedEntity, portLinks) in sourceComponent.LinkedPorts)
        {
            foreach (var (sourcePort, sinkPort) in portLinks)
            {
                if (sourcePort == loader.Comp.LoadPort)
                {
                    if (HasComp<SpaceArtilleryComponent>(linkedEntity))
                    {
                        if (!linkedArtillery.Contains(linkedEntity))
                        {
                            linkedArtillery.Add(linkedEntity);
                        }

                        break;
                    }
                }
            }
        }

        return linkedArtillery;
    }

    private bool IsAmmoCompatible(Entity<AmmoLoaderComponent> loader, EntityUid artillery, EntityUid ammoEntity)
    {
        if (!_gun.TryGetGun(artillery, out var gunUid, out _))
            return false;

        if (TryComp<MagazineAmmoProviderComponent>(gunUid, out _))
        {
            if (TryComp<BallisticAmmoProviderComponent>(ammoEntity, out _))
            {
                if (TryComp<ItemSlotsComponent>(gunUid, out var itemSlots))
                {
                    var magazineSlot = itemSlots.Slots.GetValueOrDefault("gun_magazine");
                    if (magazineSlot != null)
                    {
                        return !_whitelistSystem.IsWhitelistFailOrNull(magazineSlot.Whitelist, ammoEntity) &&
                               !_whitelistSystem.IsBlacklistPass(magazineSlot.Blacklist, ammoEntity);
                    }
                }
                return false;
            }
        }

        if (TryComp<BallisticAmmoProviderComponent>(gunUid, out var artilleryAmmo))
        {
            if (TryComp<BallisticAmmoProviderComponent>(ammoEntity, out _))
                return false;

            if (HasComp<AmmoComponent>(ammoEntity) || HasComp<CartridgeAmmoComponent>(ammoEntity))
            {
                return !_whitelistSystem.IsWhitelistFailOrNull(artilleryAmmo.Whitelist, ammoEntity);
            }
        }

        return false;
    }

    public bool TryTransferAmmoTo(Entity<AmmoLoaderComponent> loader, EntityUid artillery)
    {
        if (loader.Comp.Container.ContainedEntities.Count == 0)
            return false;

        var successCount = 0;

        foreach (var ammoEntity in loader.Comp.Container.ContainedEntities.ToArray())
        {
            if (!IsAmmoCompatible(loader, artillery, ammoEntity))
                continue;

            if (TryTransferSingleAmmo(loader, artillery, ammoEntity))
            {
                successCount++;
            }
        }

        return successCount > 0;
    }

    private bool TryTransferSingleAmmo(Entity<AmmoLoaderComponent> loader, EntityUid artillery, EntityUid ammoEntity)
    {
        if (!_gun.TryGetGun(artillery, out var gunUid, out _))
            return false;

        if (TryComp<MagazineAmmoProviderComponent>(gunUid, out _))
        {
            if (TryComp<BallisticAmmoProviderComponent>(ammoEntity, out _))
            {
                _containers.Remove(ammoEntity, loader.Comp.Container);

                if (TryComp<ItemSlotsComponent>(gunUid, out var itemSlots))
                {
                    var magazineSlot = itemSlots.Slots.GetValueOrDefault("gun_magazine");
                    if (magazineSlot != null)
                    {
                        if (magazineSlot.HasItem)
                        {
                            _slots.TryEject(gunUid, "gun_magazine", null, out var ejectedMag, excludeUserAudio: true);
                        }

                        if (_slots.TryInsert(gunUid, magazineSlot, ammoEntity, null, excludeUserAudio: true))
                        {
                            return true;
                        }
                    }
                }

                _containers.Insert(ammoEntity, loader.Comp.Container);
                return false;
            }
        }

        if (!TryComp<BallisticAmmoProviderComponent>(gunUid, out var artilleryAmmo))
            return false;

        if (TryComp<BallisticAmmoProviderComponent>(ammoEntity, out var magazineAmmoProvider))
        {
            _containers.Remove(ammoEntity, loader.Comp.Container);

            foreach (var existingAmmo in artilleryAmmo.Container.ContainedEntities.ToArray())
            {
                _containers.Remove(existingAmmo, artilleryAmmo.Container);
                Del(existingAmmo);
            }

            foreach (var bullet in magazineAmmoProvider.Container.ContainedEntities.ToArray())
            {
                if (artilleryAmmo.Count >= artilleryAmmo.Capacity)
                    break;

                _containers.Remove(bullet, magazineAmmoProvider.Container);
                _containers.Insert(bullet, artilleryAmmo.Container);
            }

            Del(ammoEntity);

            Dirty(gunUid, artilleryAmmo);
            return true;
        }

        if (artilleryAmmo.Count >= artilleryAmmo.Capacity)
        {
            _containers.Remove(ammoEntity, loader.Comp.Container);
            Dirty(loader, loader.Comp);
            return false;
        }

        if (HasComp<AmmoComponent>(ammoEntity))
        {
            _containers.Remove(ammoEntity, loader.Comp.Container);
            _containers.Insert(ammoEntity, artilleryAmmo.Container);
            Dirty(gunUid, artilleryAmmo);
            return true;
        }

        if (HasComp<CartridgeAmmoComponent>(ammoEntity))
        {
            _containers.Remove(ammoEntity, loader.Comp.Container);
            _gun.AddBallisticAmmo(gunUid, artilleryAmmo, 1);
            Del(ammoEntity);
            return true;
        }

        return false;
    }
}

