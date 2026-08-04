using System.Linq;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Lua.AmmoLoader;
using Content.Shared._Mono.AmmoLoader;
using Content.Server._Mono.SpaceArtillery.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.AmmoLoader;

public sealed partial class AmmoLoaderSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private DeviceLinkSystem _deviceLink = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private GunSystem _gun = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AmmoLoaderComponent, ComponentInit>(OnComponentInit);
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

    private void OnAfterInteractUsing(Entity<AmmoLoaderComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (AmmoLoaderCapacity.GetStoredAmmoUnitCount(EntityManager, ent.Comp) +
            AmmoLoaderCapacity.GetStoredAmmoUnitCount(EntityManager, args.Used) > ent.Comp.MaxCapacity)
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

        if (AmmoLoaderCapacity.GetStoredAmmoUnitCount(EntityManager, component) +
            AmmoLoaderCapacity.GetStoredAmmoUnitCount(EntityManager, entity) > component.MaxCapacity)
            return false;

        if (AmmoLoaderCapacity.IsEmptyAmmoContainer(EntityManager, entity))
            return false;

        if (!HasComp<BallisticAmmoProviderComponent>(entity) &&
            !HasComp<AmmoComponent>(entity) &&
            !HasComp<CartridgeAmmoComponent>(entity))
            return false;

        return true;
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

    public IReadOnlyList<EntityUid> GetLinkedArtillery(Entity<AmmoLoaderComponent> loader)
    {
        var linkedArtillery = new List<EntityUid>();

        if (!TryComp<DeviceLinkSourceComponent>(loader, out var sourceComponent))
            return linkedArtillery;

        foreach (var (linkedEntity, portLinks) in sourceComponent.LinkedPorts)
        {
            if (!Exists(linkedEntity) || portLinks.Count == 0) continue;
            var isArtillery = HasComp<SpaceArtilleryComponent>(linkedEntity) || (HasComp<GunComponent>(linkedEntity) && HasComp<DeviceLinkSinkComponent>(linkedEntity));
            if (!isArtillery) continue;
            if (!linkedArtillery.Contains(linkedEntity)) linkedArtillery.Add(linkedEntity);
        }
        return linkedArtillery;
    }

    public bool IsTurretLinked(Entity<AmmoLoaderComponent> loader, EntityUid artillery)
    { return GetLinkedArtillery(loader).Contains(artillery); }

    public bool TryGetTurretAmmoState(
        EntityUid artillery,
        out EntProtoId? loadedAmmoPrototype,
        out int ammoCount,
        out int ammoCapacity,
        out bool canModifyAmmo)
    {
        loadedAmmoPrototype = null;
        ammoCount = 0;
        ammoCapacity = 0;
        canModifyAmmo = true;

        if (!_gun.TryGetGun(artillery, out var gunUid, out _))
            return false;

        if (TryComp<MagazineAmmoProviderComponent>(gunUid, out _))
        {
            if (TryComp<ItemSlotsComponent>(gunUid, out var itemSlots))
            {
                var magazineSlot = itemSlots.Slots.GetValueOrDefault("gun_magazine");
                if (magazineSlot?.Item is { } magazine)
                    loadedAmmoPrototype = MetaData(magazine).EntityPrototype?.ID;
            }
            var ev = new GetAmmoCountEvent();
            RaiseLocalEvent(gunUid, ref ev, false);
            ammoCount = ev.Count;
            ammoCapacity = ev.Capacity;
            return true;
        }

        if (!TryComp<BallisticAmmoProviderComponent>(gunUid, out var artilleryAmmo))
            return false;

        ammoCount = artilleryAmmo.Count;
        ammoCapacity = artilleryAmmo.Capacity;
        if (artilleryAmmo.Container.ContainedEntities.Count > 0)
        { loadedAmmoPrototype = MetaData(artilleryAmmo.Container.ContainedEntities[0]).EntityPrototype?.ID; }
        else if (artilleryAmmo.Proto != null && ammoCount > 0)
        {
            loadedAmmoPrototype = artilleryAmmo.Proto;
        }

        return true;
    }

    public bool TryLoadAmmoToTurret(
        Entity<AmmoLoaderComponent> loader,
        EntityUid artillery,
        EntProtoId ammoPrototypeId,
        EntityUid? user = null)
    {
        if (!Transform(loader).Anchored)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("ammo-loader-not-anchored"), loader, user.Value);

            return false;
        }

        if (!IsTurretLinked(loader, artillery))
            return false;

        if (!TryGetTurretAmmoState(artillery, out _, out _, out _, out var canModifyAmmo) || !canModifyAmmo)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("ammo-loader-turret-locked"), loader, user.Value);

            return false;
        }

        EjectEmptyContainers(loader, user);

        EntityUid? ammoEntity = null;
        foreach (var contained in loader.Comp.Container.ContainedEntities)
        {
            if (MetaData(contained).EntityPrototype?.ID != ammoPrototypeId.Id)
                continue;
            if (AmmoLoaderCapacity.IsEmptyAmmoContainer(EntityManager, contained))
                continue;
            ammoEntity = contained;
            break;
        }

        if (ammoEntity == null)
            return false;

        if (!IsAmmoCompatible(loader, artillery, ammoEntity.Value))
        {
            if (user != null) _popup.PopupEntity(Loc.GetString("ammo-loader-incompatible-ammo"), loader, user.Value);
            return false;
        }
        if (!TryPrepareTurretForLoad(loader, artillery, ammoEntity.Value, user)) return false;
        if (TryTransferSingleAmmo(loader, artillery, ammoEntity.Value)) return true;
        if (user != null) _popup.PopupEntity(Loc.GetString("ammo-loader-load-failed"), loader, user.Value);

        return false;
    }

    public bool TryUnloadTurretToLoader(
        Entity<AmmoLoaderComponent> loader,
        EntityUid artillery,
        EntityUid? user = null)
    {
        if (!Transform(loader).Anchored)
        {
            if (user != null) _popup.PopupEntity(Loc.GetString("ammo-loader-not-anchored"), loader, user.Value);
            return false;
        }

        if (!IsTurretLinked(loader, artillery))
            return false;

        if (!TryGetTurretAmmoState(artillery, out _, out var ammoCount, out _, out var canModifyAmmo) ||
            !canModifyAmmo ||
            ammoCount == 0)
        {
            if (user != null && !canModifyAmmo)
                _popup.PopupEntity(Loc.GetString("ammo-loader-turret-locked"), loader, user.Value);

            return false;
        }

        if (!TryUnloadTurretAmmoToLoader(loader, artillery, user))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("ammo-loader-unload-failed"), loader, user.Value);

            return false;
        }

        return true;
    }

    private bool TryPrepareTurretForLoad(
        Entity<AmmoLoaderComponent> loader,
        EntityUid artillery,
        EntityUid incomingAmmo,
        EntityUid? user)
    {
        if (!_gun.TryGetGun(artillery, out var gunUid, out _))
            return false;

        if (TryComp<MagazineAmmoProviderComponent>(gunUid, out _) &&
            HasComp<BallisticAmmoProviderComponent>(incomingAmmo))
        {
            return true;
        }

        if (!TryComp<BallisticAmmoProviderComponent>(gunUid, out var artilleryAmmo))
            return true;

        if (artilleryAmmo.Count == 0)
            return true;
        if (TryComp<BallisticAmmoProviderComponent>(incomingAmmo, out _))
        {
            if (artilleryAmmo.Count >= artilleryAmmo.Capacity)
                return TryUnloadTurretAmmoToLoader(loader, artillery, user);

            return true;
        }

        var incomingProto = MetaData(incomingAmmo).EntityPrototype?.ID;
        if (incomingProto != null &&
            artilleryAmmo.Container.ContainedEntities.Count > 0 &&
            MetaData(artilleryAmmo.Container.ContainedEntities[0]).EntityPrototype?.ID == incomingProto &&
            artilleryAmmo.Count < artilleryAmmo.Capacity)
        {
            return true;
        }

        return TryUnloadTurretAmmoToLoader(loader, artillery, user);
    }

    private bool TryUnloadTurretAmmoToLoader(
        Entity<AmmoLoaderComponent> loader,
        EntityUid artillery,
        EntityUid? user)
    {
        if (!_gun.TryGetGun(artillery, out var gunUid, out _))
            return false;

        if (TryComp<MagazineAmmoProviderComponent>(gunUid, out _))
        {
            if (!TryComp<ItemSlotsComponent>(gunUid, out var itemSlots))
                return false;

            var magazineSlot = itemSlots.Slots.GetValueOrDefault("gun_magazine");
            if (magazineSlot?.Item == null)
                return false;

            if (!_slots.TryEject(gunUid, "gun_magazine", null, out var magazine, excludeUserAudio: true) ||
                magazine == null)
                return false;

            if (!TryInsertIntoLoader(loader, magazine.Value, user))
            {
                if (!_slots.TryInsert(gunUid, magazineSlot, magazine.Value, null, excludeUserAudio: true))
                    Del(magazine.Value);

                return false;
            }

            return true;
        }

        if (!TryComp<BallisticAmmoProviderComponent>(gunUid, out var artilleryAmmo))
            return false;
        var shots = artilleryAmmo.InfiniteUnspawned ? artilleryAmmo.Container.ContainedEntities.Count : artilleryAmmo.Count;
        if (shots <= 0) return false;
        var taken = new List<(EntityUid? Entity, IShootable Shootable)>(shots);
        var takeEv = new TakeAmmoEvent(shots, taken, Transform(gunUid).Coordinates, user);
        RaiseLocalEvent(gunUid, takeEv);

        var unloaded = false;
        foreach (var (ent, _) in taken)
        {
            if (ent == null)
                continue;

            if (!TryInsertIntoLoader(loader, ent.Value, user))
            {
                _containers.Insert(ent.Value, artilleryAmmo.Container);
                _gun.AddBallisticAmmo((gunUid, artilleryAmmo), ent.Value);
                return unloaded;
            }

            unloaded = true;
        }

        return unloaded;
    }

    private bool TryInsertIntoLoader(Entity<AmmoLoaderComponent> loader, EntityUid item, EntityUid? user)
    {
        if (AmmoLoaderCapacity.IsEmptyAmmoContainer(EntityManager, item))
        {
            PlaceOutsideLoader(loader, item, user);
            return true;
        }

        var incomingUnits = AmmoLoaderCapacity.GetStoredAmmoUnitCount(EntityManager, item);
        var currentUnits = AmmoLoaderCapacity.GetStoredAmmoUnitCount(EntityManager, loader.Comp);

        if (currentUnits + incomingUnits > loader.Comp.MaxCapacity)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("ammo-loader-insert-fail"), loader, user.Value);

            return false;
        }

        return _containers.Insert(item, loader.Comp.Container);
    }
    public void EjectEmptyContainers(Entity<AmmoLoaderComponent> loader, EntityUid? user = null)
    {
        foreach (var contained in loader.Comp.Container.ContainedEntities.ToArray())
        {
            if (!AmmoLoaderCapacity.IsEmptyAmmoContainer(EntityManager, contained)) continue;
            PlaceOutsideLoader(loader, contained, user);
        }
    }

    private void PlaceOutsideLoader(EntityUid loader, EntityUid item, EntityUid? user)
    {
        if (TryComp<AmmoLoaderComponent>(loader, out var comp) &&
            comp.Container.ContainedEntities.Contains(item))
        {
            _containers.Remove(item, comp.Container);
        }

        if (user != null)
        {
            _hands.PickupOrDrop(user.Value, item);
            return;
        }

        _transform.DropNextTo(item, loader);
    }

    private bool TryEjectMagazineToLoader(
        Entity<AmmoLoaderComponent> loader,
        EntityUid gunUid,
        ItemSlot magazineSlot,
        EntityUid? user)
    {
        if (!magazineSlot.HasItem)
            return true;

        if (!_slots.TryEject(gunUid, "gun_magazine", null, out var ejectedMag, excludeUserAudio: true) ||
            ejectedMag == null)
            return false;

        if (TryInsertIntoLoader(loader, ejectedMag.Value, user))
            return true;

        if (!_slots.TryInsert(gunUid, magazineSlot, ejectedMag.Value, null, excludeUserAudio: true))
            Del(ejectedMag.Value);

        return false;
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
            if (TryComp<BallisticAmmoProviderComponent>(ammoEntity, out var boxAmmo))
                return IsBallisticBoxCompatible(artilleryAmmo, boxAmmo, ammoEntity);

            if (HasComp<AmmoComponent>(ammoEntity) || HasComp<CartridgeAmmoComponent>(ammoEntity))
                return !_whitelistSystem.IsWhitelistFailOrNull(artilleryAmmo.Whitelist, ammoEntity);
        }

        return false;
    }

    private bool IsBallisticBoxCompatible(
        BallisticAmmoProviderComponent gunAmmo,
        BallisticAmmoProviderComponent boxAmmo,
        EntityUid boxUid)
    {
        foreach (var bullet in boxAmmo.Container.ContainedEntities)
        {
            if (!_whitelistSystem.IsWhitelistFailOrNull(gunAmmo.Whitelist, bullet))
                return true;
        }

        var gunTags = gunAmmo.Whitelist?.Tags;
        var boxTags = boxAmmo.Whitelist?.Tags;

        if (gunTags == null || gunTags.Count == 0)
            return boxAmmo.Count > 0 || boxAmmo.Proto != null;

        if (boxTags == null)
            return false;

        foreach (var boxTag in boxTags)
        {
            foreach (var gunTag in gunTags)
            {
                if (boxTag == gunTag)
                    return true;
            }
        }

        return false;
    }

    public bool TryTransferAmmoTo(Entity<AmmoLoaderComponent> loader, EntityUid artillery)
    {
        if (loader.Comp.Container.ContainedEntities.Count == 0)
            return false;

        var successCount = 0;

        EjectEmptyContainers(loader);

        foreach (var ammoEntity in loader.Comp.Container.ContainedEntities.ToArray())
        {
            if (AmmoLoaderCapacity.IsEmptyAmmoContainer(EntityManager, ammoEntity))
                continue;

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
                        if (!TryEjectMagazineToLoader(loader, gunUid, magazineSlot, null))
                        {
                            _containers.Insert(ammoEntity, loader.Comp.Container);
                            return false;
                        }

                        if (_slots.TryInsert(gunUid, magazineSlot, ammoEntity, null, excludeUserAudio: true))
                            return true;
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

            var transferred = 0;
            while (artilleryAmmo.Count < artilleryAmmo.Capacity && magazineAmmoProvider.Count > 0)
            {
                var taken = new List<(EntityUid? Entity, IShootable Shootable)>(1);
                var takeEv = new TakeAmmoEvent(1, taken, Transform(ammoEntity).Coordinates, null);
                RaiseLocalEvent(ammoEntity, takeEv);

                if (taken.Count == 0 || taken[0].Entity is not { } bullet)
                    break;

                if (_whitelistSystem.IsWhitelistFailOrNull(artilleryAmmo.Whitelist, bullet))
                {
                    _containers.Insert(bullet, magazineAmmoProvider.Container);
                    _gun.AddBallisticAmmo((ammoEntity, magazineAmmoProvider), bullet);
                    break;
                }

                _containers.Insert(bullet, artilleryAmmo.Container);
                _gun.AddBallisticAmmo((gunUid, artilleryAmmo), bullet);
                transferred++;
            }
            TryInsertIntoLoader(loader, ammoEntity, null);

            return transferred > 0;
        }

        if (artilleryAmmo.Count >= artilleryAmmo.Capacity)
            return false;

        if (HasComp<AmmoComponent>(ammoEntity) || HasComp<CartridgeAmmoComponent>(ammoEntity))
        {
            _containers.Remove(ammoEntity, loader.Comp.Container);
            _containers.Insert(ammoEntity, artilleryAmmo.Container);
            _gun.AddBallisticAmmo((gunUid, artilleryAmmo), ammoEntity);
            return true;
        }

        return false;
    }
}
//hard mode lua
