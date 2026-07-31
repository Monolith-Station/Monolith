// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Linq;
using Content.Server._Mono.AmmoLoader;
using Content.Shared._Lua.AmmoLoader;
using Content.Shared._Mono.AmmoLoader;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Lua.AmmoLoader;

public sealed class AmmoLoaderUiSystem : EntitySystem
{
    [Dependency] private readonly AmmoLoaderSystem _ammoLoader = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AmmoLoaderComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<AmmoLoaderComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<AmmoLoaderComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<AmmoLoaderComponent, NewLinkEvent>(OnLinkChanged);
        SubscribeLocalEvent<AmmoLoaderComponent, PortDisconnectedEvent>(OnPortDisconnected);

        Subs.BuiEvents<AmmoLoaderComponent>(AmmoLoaderUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<AmmoLoaderUnloadOneMessage>(OnUnloadOne);
            subs.Event<AmmoLoaderEjectAllMessage>(OnEjectAll);
            subs.Event<AmmoLoaderLoadTurretMessage>(OnLoadTurret);
            subs.Event<AmmoLoaderUnloadTurretMessage>(OnUnloadTurret);
        });
    }

    private void OnUiOpened(Entity<AmmoLoaderComponent> ent, ref BoundUIOpenedEvent args)
    {
        _ammoLoader.EjectEmptyContainers(ent, args.Actor);
        UpdateUi(ent);
    }

    private void OnLinkChanged(Entity<AmmoLoaderComponent> ent, ref NewLinkEvent args)
    {
        if (args.Source != ent.Owner)
            return;

        UpdateUi(ent);
    }

    private void OnPortDisconnected(Entity<AmmoLoaderComponent> ent, ref PortDisconnectedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnEntInserted(Entity<AmmoLoaderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != AmmoLoaderComponent.ContainerId)
            return;
        if (AmmoLoaderCapacity.IsEmptyAmmoContainer(EntityManager, args.Entity))
            _ammoLoader.EjectEmptyContainers(ent);

        UpdateUi(ent);
    }

    private void OnEntRemoved(Entity<AmmoLoaderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != AmmoLoaderComponent.ContainerId)
            return;

        UpdateUi(ent);
    }

    private void OnInsertAttempt(Entity<AmmoLoaderComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID != AmmoLoaderComponent.ContainerId)
            return;

        if (AmmoLoaderCapacity.GetStoredAmmoUnitCount(EntityManager, ent.Comp) +
            AmmoLoaderCapacity.GetStoredAmmoUnitCount(EntityManager, args.EntityUid) > ent.Comp.MaxCapacity)
        {
            args.Cancel();
            return;
        }
        if (AmmoLoaderCapacity.IsEmptyAmmoContainer(EntityManager, args.EntityUid))
        {
            args.Cancel();
            return;
        }

        if (!Transform(ent).Anchored)
        {
            args.Cancel();
            return;
        }

        if (!HasComp<BallisticAmmoProviderComponent>(args.EntityUid) &&
            !HasComp<AmmoComponent>(args.EntityUid) &&
            !HasComp<CartridgeAmmoComponent>(args.EntityUid))
        {
            args.Cancel();
        }
    }

    private void OnUnloadOne(Entity<AmmoLoaderComponent> ent, ref AmmoLoaderUnloadOneMessage args)
    {
        EntityUid? match = null;

        foreach (var contained in ent.Comp.Container.ContainedEntities)
        {
            var protoId = MetaData(contained).EntityPrototype?.ID;
            if (protoId != args.PrototypeId.Id)
                continue;

            var isEmpty = AmmoLoaderCapacity.IsEmptyAmmoContainer(EntityManager, contained);
            if (args.EmptyOnly != isEmpty)
                continue;

            if (!_containers.CanRemove(contained, ent.Comp.Container))
                continue;

            match = contained;
            break;
        }

        if (match == null)
            return;

        if (!_containers.Remove(match.Value, ent.Comp.Container))
            return;

        _hands.PickupOrDrop(args.Actor, match.Value);
        UpdateUi(ent);
    }

    private void OnEjectAll(Entity<AmmoLoaderComponent> ent, ref AmmoLoaderEjectAllMessage args)
    {
        foreach (var entity in ent.Comp.Container.ContainedEntities.ToArray())
            _containers.Remove(entity, ent.Comp.Container);

        UpdateUi(ent);
    }

    private void OnLoadTurret(Entity<AmmoLoaderComponent> ent, ref AmmoLoaderLoadTurretMessage args)
    {
        if (!TryGetEntity(args.Turret, out var turret) || turret == null)
            return;

        if (_ammoLoader.TryLoadAmmoToTurret(ent, turret.Value, args.AmmoPrototypeId, args.Actor))
            UpdateUi(ent);
    }

    private void OnUnloadTurret(Entity<AmmoLoaderComponent> ent, ref AmmoLoaderUnloadTurretMessage args)
    {
        if (!TryGetEntity(args.Turret, out var turret) || turret == null)
            return;

        if (_ammoLoader.TryUnloadTurretToLoader(ent, turret.Value, args.Actor))
            UpdateUi(ent);
    }

    private void UpdateUi(Entity<AmmoLoaderComponent> ent)
    {
        var filledGroups = new Dictionary<string, int>();
        var emptyGroups = new Dictionary<string, int>();
        var currentCount = 0;

        foreach (var contained in ent.Comp.Container.ContainedEntities)
        {
            var protoId = MetaData(contained).EntityPrototype?.ID;
            if (protoId == null)
                continue;

            if (AmmoLoaderCapacity.IsEmptyAmmoContainer(EntityManager, contained))
            {
                emptyGroups.TryGetValue(protoId, out var emptyCount);
                emptyGroups[protoId] = emptyCount + 1;
                continue;
            }

            var unitCount = AmmoLoaderCapacity.GetStoredAmmoUnitCount(EntityManager, contained);
            currentCount += unitCount;

            filledGroups.TryGetValue(protoId, out var count);
            filledGroups[protoId] = count + unitCount;
        }

        var groupList = filledGroups
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new AmmoLoaderInventoryGroup(kvp.Key, kvp.Value))
            .Concat(emptyGroups
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => new AmmoLoaderInventoryGroup(kvp.Key, kvp.Value, isEmpty: true)))
            .ToList();

        var linkedTurrets = new List<AmmoLoaderLinkedTurret>();
        foreach (var turret in _ammoLoader.GetLinkedArtillery(ent))
        {
            var meta = MetaData(turret);
            var turretProto = meta.EntityPrototype?.ID;
            if (string.IsNullOrEmpty(turretProto))
                continue;

            _ammoLoader.TryGetTurretAmmoState(
                turret,
                out var loadedAmmo,
                out var ammoCount,
                out var ammoCapacity,
                out var canModifyAmmo);

            linkedTurrets.Add(new AmmoLoaderLinkedTurret(
                GetNetEntity(turret),
                turretProto,
                meta.EntityName,
                loadedAmmo,
                ammoCount,
                ammoCapacity,
                canModifyAmmo));
        }

        var state = new AmmoLoaderBoundUserInterfaceState(
            groupList,
            linkedTurrets,
            currentCount,
            ent.Comp.MaxCapacity,
            ent.Comp.MaxConnections);
        _ui.SetUiState(ent.Owner, AmmoLoaderUiKey.Key, state);
    }
}
