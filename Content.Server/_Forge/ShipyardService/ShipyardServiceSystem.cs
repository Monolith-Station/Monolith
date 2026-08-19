using Content.Server._Mono.ShipRepair;
using Content.Server._NF.Bank;
using Content.Server.Cargo.Systems;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Stack;
using Content.Shared._Forge.ShipyardService;
using Content.Shared._Forge.ShipyardService.Components;
using Content.Shared._Mono.ShipRepair.Components;
using Content.Shared._Mono.Ships.Components;
using Content.Shared._NF.Bank.Components;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Construction.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Station;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Forge.ShipyardService;

public sealed class ShipyardServiceSystem : EntitySystem
{
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly ConstructionSystem _construction = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ShipRepairSystem _shipRepair = default!;
    [Dependency] private readonly StackSystem _stacks = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly ProtoId<TagPrototype> WallT1Tag = "WallT1";
    private static readonly ProtoId<TagPrototype> WallT2Tag = "WallT2";
    private static readonly ProtoId<TagPrototype> WallT3Tag = "WallT3";
    private static readonly ProtoId<TagPrototype> DiagonalTag = "Diagonal";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipyardDockComponent, DockEvent>(OnDock);
        SubscribeLocalEvent<ShipyardDockComponent, UndockEvent>(OnUndock);
        SubscribeLocalEvent<ShipyardDockComponent, ExaminedEvent>(OnDockExamined);

        SubscribeLocalEvent<ShipyardServiceConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ShipyardServiceConsoleComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<ShipyardServiceConsoleComponent, ShipyardServiceSelectMessage>(OnSelectShuttle);
        SubscribeLocalEvent<ShipyardServiceConsoleComponent, ShipyardServicePurchaseMessage>(OnPurchase);
        SubscribeLocalEvent<ShipyardServiceConsoleComponent, ShipyardServiceUpgradeMarkedMessage>(OnUpgradeMarked);
        SubscribeLocalEvent<ShipyardServiceUserComponent, ShipyardServiceUpgradeTargetEvent>(OnUpgradeTarget);
        SubscribeLocalEvent<ShipyardServiceUserComponent, ComponentShutdown>(OnUserShutdown);

        SubscribeLocalEvent<ShipRepairableComponent, DamageChangedEvent>(OnRepairableDamaged);
    }

    #region Docking

    private void OnDock(Entity<ShipyardDockComponent> ent, ref DockEvent args)
    {
        var shuttle = GetShuttleFromDock(ent, args);
        if (shuttle == null)
            return;

        var (fee, name) = GetDockingFee(shuttle.Value, ent.Comp.FeePercent);
        ent.Comp.OccupancyCharged = fee <= 0;
        ent.Comp.CachedFee = fee;
        ent.Comp.CachedShuttleName = name;
        Dirty(ent);
        RefreshAllConsoles();
    }

    private void OnUndock(Entity<ShipyardDockComponent> ent, ref UndockEvent args)
    {
        ent.Comp.OccupancyCharged = false;
        ent.Comp.CachedFee = 0;
        ent.Comp.CachedShuttleName = string.Empty;
        Dirty(ent);
        RefreshAllConsoles();
    }

    private void OnDockExamined(Entity<ShipyardDockComponent> ent, ref ExaminedEvent args)
    {
        if (!TryComp<DockingComponent>(ent, out var docking) || docking.DockedWith == null)
        {
            args.PushMarkup(Loc.GetString("shipyard-service-dock-examine-idle"));
            return;
        }

        if (ent.Comp.OccupancyCharged)
        {
            args.PushMarkup(Loc.GetString("shipyard-service-dock-examine-paid", ("name", ent.Comp.CachedShuttleName)));
            return;
        }

        args.PushMarkup(Loc.GetString("shipyard-service-dock-examine-unpaid",
            ("amount", ent.Comp.CachedFee),
            ("name", ent.Comp.CachedShuttleName)));
    }

    #endregion

    #region Console

    private void OnUiOpened(Entity<ShipyardServiceConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        EnsureUpgradeAction(args.Actor, ent);
        RefreshUi(ent, args.Actor);
    }

    private void OnUiClosed(Entity<ShipyardServiceConsoleComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!TryComp<ShipyardServiceUserComponent>(args.Actor, out var user) || user.Console != ent.Owner)
            return;

        RemComp<ShipyardServiceUserComponent>(args.Actor);
    }

    private void OnSelectShuttle(Entity<ShipyardServiceConsoleComponent> ent, ref ShipyardServiceSelectMessage args)
    {
        if (!TryGetEntity(args.Shuttle, out var shuttle) || !IsDockedShuttle(ent, shuttle.Value))
            return;

        ent.Comp.SelectedShuttle = shuttle;
        RefreshUi(ent, args.Actor);
    }

    private void OnPurchase(Entity<ShipyardServiceConsoleComponent> ent, ref ShipyardServicePurchaseMessage args)
    {
        var actor = args.Actor;
        if (!TryGetSelectedShuttle(ent, out var shuttle))
        {
            _popup.PopupEntity(Loc.GetString("shipyard-service-no-shuttle"), ent, actor);
            return;
        }

        if (!TryComp<BankAccountComponent>(actor, out var bank))
        {
            _popup.PopupEntity(Loc.GetString("shipyard-service-no-bank"), ent, actor);
            return;
        }

        var quote = BuildQuote(ent, shuttle);
        var (count, cost, ok) = args.Action switch
        {
            ShipyardServiceAction.Repair => (quote.RepairCount, quote.RepairCost, !quote.RepairOnCooldown && quote.RepairCount > 0),
            ShipyardServiceAction.UpgradeParts => (quote.PartCount, quote.PartCost, quote.PartCount > 0),
            ShipyardServiceAction.Reinforce => (quote.ReinforceCount, quote.ReinforceCost, quote.ReinforceCount > 0),
            ShipyardServiceAction.Plastitanium => (quote.PlastitaniumCount, quote.PlastitaniumCost, quote.PlastitaniumCount > 0),
            _ => (0, 0, false)
        };

        if (!ok || cost <= 0)
        {
            if (args.Action == ShipyardServiceAction.Repair && quote.RepairOnCooldown)
            {
                _popup.PopupEntity(Loc.GetString("shipyard-service-repair-cooldown"), ent, actor);
                return;
            }

            _popup.PopupEntity(Loc.GetString("shipyard-service-nothing-to-do"), ent, actor);
            return;
        }

        if (bank.Balance < cost || !_bank.TryBankWithdraw(actor, cost))
        {
            _popup.PopupEntity(Loc.GetString("shipyard-service-insufficient-funds", ("amount", cost)), ent, actor);
            return;
        }

        var applied = args.Action switch
        {
            ShipyardServiceAction.Repair => ApplyRepair(shuttle),
            ShipyardServiceAction.UpgradeParts => ApplyPartUpgrades(ent, shuttle),
            ShipyardServiceAction.Reinforce => ApplyStructureUpgrades(ent, shuttle, regular: true),
            ShipyardServiceAction.Plastitanium => ApplyStructureUpgrades(ent, shuttle, regular: false),
            _ => 0
        };

        if (args.Action == ShipyardServiceAction.Repair)
            MarkOccupancyCharged(shuttle);

        _adminLog.Add(LogType.ShipYardUsage, LogImpact.Low,
            $"{ToPrettyString(actor):player} bought shipyard {args.Action} ({applied}/{count}) for {cost} on {ToPrettyString(shuttle)} via {ToPrettyString(ent)}");
        _popup.PopupEntity(Loc.GetString("shipyard-service-purchase-complete",
            ("action", Loc.GetString($"shipyard-service-action-{args.Action.ToString().ToLowerInvariant()}")),
            ("count", applied),
            ("amount", cost)), ent, actor);

        RefreshUi(ent, actor);
    }

    private void OnUpgradeMarked(Entity<ShipyardServiceConsoleComponent> ent, ref ShipyardServiceUpgradeMarkedMessage args)
    {
        var actor = args.Actor;
        if (!TryGetSelectedShuttle(ent, out var shuttle))
        {
            _popup.PopupEntity(Loc.GetString("shipyard-service-no-shuttle"), ent, actor);
            return;
        }

        if (!TryComp<BankAccountComponent>(actor, out var bank))
        {
            _popup.PopupEntity(Loc.GetString("shipyard-service-no-bank"), ent, actor);
            return;
        }

        var current = CollectMarkers(ent, shuttle);
        var jobs = new List<ShipyardServiceUpgradeMarker>();
        var seen = new HashSet<(NetEntity Entity, Vector2i Tile, ShipyardServiceAction Action, bool IsTile)>();
        var repairCost = 0;
        var upgradeCost = 0;
        var totalCount = 0;
        var hasRepair = false;

        foreach (var key in args.Targets)
        {
            var seenKey = (key.Entity, key.Tile, key.Action, key.IsTile);
            if (!seen.Add(seenKey))
                continue;

            ShipyardServiceUpgradeMarker? match = null;
            foreach (var marker in current)
            {
                if (marker.Action == key.Action &&
                    marker.IsTile == key.IsTile &&
                    marker.Tile == key.Tile &&
                    marker.Entity == key.Entity)
                {
                    match = marker;
                    break;
                }
            }

            if (match == null)
                continue;

            if (match.Action == ShipyardServiceAction.Repair)
            {
                hasRepair = true;
                repairCost += match.Cost;
            }
            else
            {
                upgradeCost += match.Cost;
            }

            jobs.Add(match);
            totalCount += match.Count;
        }

        GetVesselInfo(shuttle, out _, out var vesselPrice, out _, out _);
        repairCost = ShipyardServicePricing.CapRepairWork(repairCost, vesselPrice);
        var occupancyCharge = 0;
        if (hasRepair && TryGetOccupancyFee(shuttle, out var occupancyFee) && occupancyFee > 0)
            occupancyCharge = occupancyFee;

        var totalCost = ShipyardServicePricing.CapRepairTotal(repairCost, occupancyCharge, vesselPrice) + upgradeCost;

        if (hasRepair)
        {
            if (GetRepairCooldownEnd(ent, shuttle) > _timing.CurTime)
            {
                _popup.PopupEntity(Loc.GetString("shipyard-service-repair-cooldown"), ent, actor);
                return;
            }
        }

        if (jobs.Count == 0 || totalCost <= 0)
        {
            _popup.PopupEntity(Loc.GetString("shipyard-service-click-nothing"), ent, actor);
            return;
        }

        if (bank.Balance < totalCost || !_bank.TryBankWithdraw(actor, totalCost))
        {
            _popup.PopupEntity(Loc.GetString("shipyard-service-insufficient-funds", ("amount", totalCost)), ent, actor);
            return;
        }

        var applied = 0;
        foreach (var job in jobs)
        {
            if (job.Action != ShipyardServiceAction.Repair)
                continue;

            EntityUid? entity = TryGetEntity(job.Entity, out var uid) ? uid : null;
            applied += _shipRepair.TryApplyRepairWork(shuttle, job.Tile, job.LocalPosition, entity, job.IsTile);
        }

        foreach (var job in jobs)
        {
            if (job.Action == ShipyardServiceAction.Repair)
                continue;

            if (!TryGetEntity(job.Entity, out var target) || TerminatingOrDeleted(target.Value))
                continue;

            applied += job.Action switch
            {
                ShipyardServiceAction.UpgradeParts => ApplyPartUpgradesOnMachine(ent, target.Value),
                ShipyardServiceAction.Reinforce => ApplyStructureUpgradeOnEntity(ent, target.Value, regular: true),
                ShipyardServiceAction.Plastitanium => ApplyStructureUpgradeOnEntity(ent, target.Value, regular: false),
                _ => 0
            };
        }

        if (hasRepair)
            MarkOccupancyCharged(shuttle);

        _adminLog.Add(LogType.ShipYardUsage, LogImpact.Low,
            $"{ToPrettyString(actor):player} marked-upgraded {applied}/{totalCount} for {totalCost} on {ToPrettyString(shuttle)} via {ToPrettyString(ent)}");
        _popup.PopupEntity(Loc.GetString("shipyard-service-purchase-complete",
            ("action", Loc.GetString("shipyard-service-action-marked")),
            ("count", applied),
            ("amount", totalCost)), ent, actor);

        RefreshUi(ent, actor);
    }

    private List<ShipyardServiceUpgradeMarker> CollectMarkers(
        Entity<ShipyardServiceConsoleComponent> console,
        EntityUid shuttle)
    {
        var list = new List<ShipyardServiceUpgradeMarker>();
        TryComp<MapGridComponent>(shuttle, out var grid);
        var nextId = 0;

        foreach (var uid in GetGridChildren(shuttle))
        {
            if (!TryGetClickUpgrade(console, shuttle, uid, out var action, out var count, out var cost))
                continue;

            var xform = Transform(uid);
            var tile = grid != null
                ? _map.LocalToTile(shuttle, grid, xform.Coordinates)
                : default;

            list.Add(new ShipyardServiceUpgradeMarker
            {
                Id = nextId++,
                Entity = GetNetEntity(uid),
                LocalPosition = xform.LocalPosition,
                Tile = tile,
                Action = action,
                Cost = cost,
                Count = count
            });
        }

        CollectRepairMarkers(console, shuttle, list, ref nextId);
        return list;
    }

    private void CollectRepairMarkers(
        Entity<ShipyardServiceConsoleComponent> console,
        EntityUid shuttle,
        List<ShipyardServiceUpgradeMarker> list,
        ref int nextId)
    {
        GetVesselInfo(shuttle, out _, out _, out var classes, out _);
        var cost = ShipyardServicePricing.ApplyMultiplier(
            console.Comp.RepairPerObjectCost,
            ShipyardServicePricing.GetRepairMultiplier(classes));
        if (cost <= 0)
            return;

        var seen = new HashSet<EntityUid>();
        var work = new List<ShipRepairWork>();
        if (TryComp<ShipRepairDataComponent>(shuttle, out var data))
            _shipRepair.CollectRepairWork(shuttle, work, data);

        foreach (var item in work)
        {
            if (item.Entity is { } entity)
                seen.Add(entity);

            list.Add(new ShipyardServiceUpgradeMarker
            {
                Id = nextId++,
                Entity = item.Entity is { } uid ? GetNetEntity(uid) : default,
                LocalPosition = item.LocalPosition,
                Tile = item.Tile,
                Action = ShipyardServiceAction.Repair,
                Cost = cost,
                Count = 1,
                IsTile = item.IsTile
            });
        }

        TryComp<MapGridComponent>(shuttle, out var grid);
        foreach (var uid in GetGridChildren(shuttle))
        {
            if (!seen.Add(uid) || HasComp<MobStateComponent>(uid))
                continue;

            if (!TryComp<DamageableComponent>(uid, out var damageable) || damageable.TotalDamage <= 0)
                continue;

            if (HasComp<ShipRepairableComponent>(uid) && HasComp<ShipRepairDataComponent>(shuttle))
                continue;

            var xform = Transform(uid);
            var tile = grid != null
                ? _map.LocalToTile(shuttle, grid, xform.Coordinates)
                : default;

            list.Add(new ShipyardServiceUpgradeMarker
            {
                Id = nextId++,
                Entity = GetNetEntity(uid),
                LocalPosition = xform.LocalPosition,
                Tile = tile,
                Action = ShipyardServiceAction.Repair,
                Cost = cost,
                Count = 1
            });
        }
    }

    private void EnsureUpgradeAction(EntityUid actor, Entity<ShipyardServiceConsoleComponent> console)
    {
        var user = EnsureComp<ShipyardServiceUserComponent>(actor);
        user.Console = console.Owner;
        if (user.ActionEntity != null && !TerminatingOrDeleted(user.ActionEntity.Value))
            return;

        _actions.AddAction(actor, ref user.ActionEntity, console.Comp.UpgradeAction);
    }

    private void OnUserShutdown(Entity<ShipyardServiceUserComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.ActionEntity != null)
            _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnUpgradeTarget(Entity<ShipyardServiceUserComponent> ent, ref ShipyardServiceUpgradeTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var actor = args.Performer;

        if (!TryComp<ShipyardServiceConsoleComponent>(ent.Comp.Console, out var consoleComp) ||
            TerminatingOrDeleted(ent.Comp.Console))
        {
            RemComp<ShipyardServiceUserComponent>(actor);
            return;
        }

        var console = new Entity<ShipyardServiceConsoleComponent>(ent.Comp.Console, consoleComp);
        if (!TryGetSelectedShuttle(console, out var shuttle))
        {
            _popup.PopupEntity(Loc.GetString("shipyard-service-no-shuttle"), actor, actor);
            return;
        }

        if (!TryComp<BankAccountComponent>(actor, out var bank))
        {
            _popup.PopupEntity(Loc.GetString("shipyard-service-no-bank"), actor, actor);
            return;
        }

        if (!TryPickClickUpgrade(console, shuttle, args.Entity, args.Coords, out var target, out var action, out var count, out var cost))
        {
            _popup.PopupEntity(Loc.GetString("shipyard-service-click-nothing"), actor, actor);
            return;
        }

        if (bank.Balance < cost || !_bank.TryBankWithdraw(actor, cost))
        {
            _popup.PopupEntity(Loc.GetString("shipyard-service-insufficient-funds", ("amount", cost)), actor, actor);
            return;
        }

        var applied = action switch
        {
            ShipyardServiceAction.UpgradeParts => ApplyPartUpgradesOnMachine(console, target),
            ShipyardServiceAction.Reinforce => ApplyStructureUpgradeOnEntity(console, target, regular: true),
            ShipyardServiceAction.Plastitanium => ApplyStructureUpgradeOnEntity(console, target, regular: false),
            _ => 0
        };

        if (applied <= 0)
        {
            _popup.PopupEntity(Loc.GetString("shipyard-service-click-nothing"), actor, actor);
            return;
        }

        _adminLog.Add(LogType.ShipYardUsage, LogImpact.Low,
            $"{ToPrettyString(actor):player} click-upgraded {ToPrettyString(target)} ({action}, {applied}) for {cost} on {ToPrettyString(shuttle)} via {ToPrettyString(console)}");
        _popup.PopupEntity(Loc.GetString("shipyard-service-purchase-complete",
            ("action", Loc.GetString($"shipyard-service-action-{action.ToString().ToLowerInvariant()}")),
            ("count", applied),
            ("amount", cost)), actor, actor);

        RefreshUi(console, actor);
    }

    private bool TryPickClickUpgrade(
        Entity<ShipyardServiceConsoleComponent> console,
        EntityUid shuttle,
        EntityUid? clicked,
        EntityCoordinates? coords,
        out EntityUid target,
        out ShipyardServiceAction action,
        out int count,
        out int cost)
    {
        target = default;
        action = default;
        count = 0;
        cost = 0;

        foreach (var uid in CollectClickCandidates(shuttle, clicked, coords))
        {
            if (TryGetClickUpgrade(console, shuttle, uid, out action, out count, out cost))
            {
                target = uid;
                return true;
            }
        }

        return false;
    }

    private List<EntityUid> CollectClickCandidates(EntityUid shuttle, EntityUid? clicked, EntityCoordinates? coords)
    {
        var list = new List<EntityUid>();
        if (clicked is { } entity &&
            entity != shuttle &&
            Exists(entity) &&
            !TerminatingOrDeleted(entity) &&
            Transform(entity).GridUid == shuttle)
            list.Add(entity);

        if (coords == null || !TryComp<MapGridComponent>(shuttle, out var grid))
            return list;

        if (_transform.GetGrid(coords.Value) != shuttle)
            return list;

        var tile = _map.CoordinatesToTile(shuttle, grid, coords.Value);
        var enumerator = _map.GetAnchoredEntitiesEnumerator(shuttle, grid, tile);
        while (enumerator.MoveNext(out var uid))
        {
            if (uid == shuttle || list.Contains(uid.Value) || TerminatingOrDeleted(uid.Value))
                continue;

            list.Add(uid.Value);
        }

        return list;
    }

    private bool TryGetClickUpgrade(
        Entity<ShipyardServiceConsoleComponent> console,
        EntityUid shuttle,
        EntityUid uid,
        out ShipyardServiceAction action,
        out int count,
        out int cost)
    {
        action = default;
        count = 0;
        cost = 0;
        if (TerminatingOrDeleted(uid) || Transform(uid).GridUid != shuttle)
            return false;

        GetVesselInfo(shuttle, out _, out _, out var classes, out _);

        if (TryGetStructureUpgrade(console, uid, regular: true, out _))
        {
            action = ShipyardServiceAction.Reinforce;
            count = 1;
            cost = ShipyardServicePricing.ApplyMultiplier(console.Comp.ReinforceCost, ShipyardServicePricing.GetReinforceMultiplier(classes));
            return cost > 0;
        }

        if (TryGetStructureUpgrade(console, uid, regular: false, out _))
        {
            action = ShipyardServiceAction.Plastitanium;
            count = 1;
            cost = ShipyardServicePricing.ApplyMultiplier(console.Comp.PlastitaniumCost, ShipyardServicePricing.GetReinforceMultiplier(classes));
            return cost > 0;
        }

        var parts = CountUpgradeablePartsOnMachine(console, uid);
        if (parts > 0)
        {
            action = ShipyardServiceAction.UpgradeParts;
            count = parts;
            cost = ShipyardServicePricing.ApplyMultiplier(console.Comp.PartUpgradeCost * parts, ShipyardServicePricing.GetPartUpgradeMultiplier(classes));
            return cost > 0;
        }

        return false;
    }

    private void RefreshAllConsoles()
    {
        var query = EntityQueryEnumerator<ShipyardServiceConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            foreach (var actor in _ui.GetActors(uid, ShipyardServiceUiKey.Key))
                RefreshUi((uid, console), actor);
        }
    }

    private void RefreshUi(Entity<ShipyardServiceConsoleComponent> ent, EntityUid actor)
    {
        TryComp<BankAccountComponent>(actor, out var bank);
        var shuttles = GetDockedShuttles(ent);
        if (ent.Comp.SelectedShuttle == null ||
            shuttles.TrueForAll(entry => GetEntity(entry.Shuttle) != ent.Comp.SelectedShuttle))
        {
            ent.Comp.SelectedShuttle = shuttles.Count > 0 ? GetEntity(shuttles[0].Shuttle) : null;
        }

        var quote = ent.Comp.SelectedShuttle is { } selected
            ? BuildQuote(ent, selected)
            : new ShipyardServiceQuote();

        _ui.SetUiState(ent.Owner, ShipyardServiceUiKey.Key, new ShipyardServiceBoundUserInterfaceState
        {
            Balance = bank?.Balance ?? 0,
            SelectedShuttle = ent.Comp.SelectedShuttle is { } selectedShuttle
                ? GetNetEntity(selectedShuttle)
                : null,
            Shuttles = shuttles,
            Quote = quote,
            Markers = ent.Comp.SelectedShuttle is { } markerShuttle
                ? CollectMarkers(ent, markerShuttle)
                : new List<ShipyardServiceUpgradeMarker>()
        });
    }

    #endregion

    #region Quotes and work

    private ShipyardServiceQuote BuildQuote(Entity<ShipyardServiceConsoleComponent> console, EntityUid shuttle)
    {
        GetVesselInfo(shuttle, out var name, out var price, out var classes, out var classLabel);
        var repairCount = CountRepairTargets(shuttle);
        var partCount = CountUpgradeableParts(console, shuttle);
        var reinforceCount = CountStructureUpgrades(console, shuttle, regular: true);
        var plastitaniumCount = CountStructureUpgrades(console, shuttle, regular: false);
        var cooldownUntil = GetRepairCooldownEnd(console, shuttle);
        var onCooldown = cooldownUntil > _timing.CurTime;
        var occupancyDue = TryGetOccupancyFee(shuttle, out var occupancyFee) && occupancyFee > 0;
        var repairWork = repairCount <= 0
            ? 0
            : ShipyardServicePricing.CapRepairWork(
                ShipyardServicePricing.ApplyMultiplier(
                    console.Comp.RepairBaseCost + console.Comp.RepairPerObjectCost * repairCount,
                    ShipyardServicePricing.GetRepairMultiplier(classes)),
                price);
        var occupancyCharge = occupancyDue && repairCount > 0 ? occupancyFee : 0;
        var repairCost = repairCount <= 0
            ? 0
            : ShipyardServicePricing.CapRepairTotal(repairWork, occupancyCharge, price);
        if (repairCost < occupancyCharge)
            occupancyCharge = repairCost;

        var repairWorkCost = Math.Max(0, repairCost - occupancyCharge);

        return new ShipyardServiceQuote
        {
            HasShuttle = true,
            ShuttleName = name,
            ClassLabel = classLabel,
            VesselPrice = price,
            OccupancyFee = occupancyCharge,
            OccupancyDue = occupancyCharge > 0,
            RepairCount = repairCount,
            RepairWorkCost = repairWorkCost,
            RepairCost = repairCost,
            RepairOnCooldown = onCooldown,
            RepairReadyAt = cooldownUntil,
            PartCount = partCount,
            PartCost = partCount <= 0
                ? 0
                : ShipyardServicePricing.ApplyMultiplier(
                    console.Comp.PartUpgradeCost * partCount,
                    ShipyardServicePricing.GetPartUpgradeMultiplier(classes)),
            ReinforceCount = reinforceCount,
            ReinforceCost = reinforceCount <= 0
                ? 0
                : ShipyardServicePricing.ApplyMultiplier(
                    console.Comp.ReinforceCost * reinforceCount,
                    ShipyardServicePricing.GetReinforceMultiplier(classes)),
            PlastitaniumCount = plastitaniumCount,
            PlastitaniumCost = plastitaniumCount <= 0
                ? 0
                : ShipyardServicePricing.ApplyMultiplier(
                    console.Comp.PlastitaniumCost * plastitaniumCount,
                    ShipyardServicePricing.GetReinforceMultiplier(classes))
        };
    }

    private int CountRepairTargets(EntityUid shuttle)
    {
        var count = 0;
        if (TryComp<ShipRepairDataComponent>(shuttle, out var data))
            count += _shipRepair.CountRepairTargets(shuttle, data);

        foreach (var uid in GetGridChildren(shuttle))
        {
            if (HasComp<MobStateComponent>(uid))
                continue;

            if (!TryComp<DamageableComponent>(uid, out var damageable) || damageable.TotalDamage <= 0)
                continue;

            if (HasComp<ShipRepairableComponent>(uid) && TryComp<ShipRepairDataComponent>(shuttle, out _))
                continue;

            count++;
        }

        return count;
    }

    private int ApplyRepair(EntityUid shuttle)
    {
        var repaired = 0;
        if (TryComp<ShipRepairDataComponent>(shuttle, out var data))
            repaired += _shipRepair.RepairFromSnapshot(shuttle, data);

        foreach (var uid in GetGridChildren(shuttle))
        {
            if (HasComp<MobStateComponent>(uid))
                continue;

            if (!TryComp<DamageableComponent>(uid, out var damageable) || damageable.TotalDamage <= 0)
                continue;

            _damageable.SetAllDamage(uid, damageable, 0);
            repaired++;
        }

        return repaired;
    }

    private int CountUpgradeableParts(Entity<ShipyardServiceConsoleComponent> console, EntityUid shuttle)
    {
        var count = 0;
        foreach (var uid in GetGridChildren(shuttle))
            count += CountUpgradeablePartsOnMachine(console, uid);

        return count;
    }

    private int CountUpgradeablePartsOnMachine(Entity<ShipyardServiceConsoleComponent> console, EntityUid uid)
    {
        if (!TryComp<MachineComponent>(uid, out var machine))
            return 0;

        var count = 0;
        foreach (var part in machine.PartContainer.ContainedEntities)
        {
            if (IsUpgradeablePart(console, part))
                count++;
        }

        return count;
    }

    private int ApplyPartUpgrades(Entity<ShipyardServiceConsoleComponent> console, EntityUid shuttle)
    {
        var upgraded = 0;
        foreach (var uid in GetGridChildren(shuttle))
            upgraded += ApplyPartUpgradesOnMachine(console, uid);

        return upgraded;
    }

    private int ApplyPartUpgradesOnMachine(Entity<ShipyardServiceConsoleComponent> console, EntityUid uid)
    {
        if (!TryComp<MachineComponent>(uid, out var machine))
            return 0;

        var existing = new List<EntityUid>(machine.PartContainer.ContainedEntities);
        var upgraded = 0;
        foreach (var part in existing)
        {
            if (!IsUpgradeablePart(console, part, out var superProto, out var stackCount))
                continue;

            var spawned = Spawn(superProto);
            if (stackCount > 1 && TryComp<StackComponent>(spawned, out var stack))
                _stacks.SetCount(spawned, stackCount, stack);

            _containers.Remove(part, machine.PartContainer);
            QueueDel(part);
            _containers.Insert(spawned, machine.PartContainer, force: true);
            upgraded++;
        }

        if (upgraded > 0)
            _construction.RefreshParts(uid, machine);

        return upgraded;
    }

    private bool IsUpgradeablePart(Entity<ShipyardServiceConsoleComponent> console, EntityUid part)
    {
        return IsUpgradeablePart(console, part, out _, out _);
    }

    private bool IsUpgradeablePart(
        Entity<ShipyardServiceConsoleComponent> console,
        EntityUid part,
        out EntProtoId superProto,
        out int stackCount)
    {
        superProto = default;
        stackCount = 1;
        if (TerminatingOrDeleted(part) || !TryComp<MachinePartComponent>(part, out var machinePart))
            return false;

        if (machinePart.Rating >= console.Comp.SuperPartRating)
            return false;

        if (!console.Comp.SuperPartPrototypes.TryGetValue(machinePart.PartType.Id, out superProto))
            return false;

        if (TryComp<StackComponent>(part, out var stack))
            stackCount = stack.Count;

        return true;
    }

    private int CountStructureUpgrades(Entity<ShipyardServiceConsoleComponent> console, EntityUid shuttle, bool regular)
    {
        var count = 0;
        foreach (var uid in GetGridChildren(shuttle))
        {
            if (TryGetStructureUpgrade(console, uid, regular, out _))
                count++;
        }

        return count;
    }

    private int ApplyStructureUpgrades(Entity<ShipyardServiceConsoleComponent> console, EntityUid shuttle, bool regular)
    {
        var toReplace = new List<(EntityUid Uid, EntProtoId Next)>();
        foreach (var uid in GetGridChildren(shuttle))
        {
            if (TryGetStructureUpgrade(console, uid, regular, out var next))
                toReplace.Add((uid, next));
        }

        var upgraded = 0;
        foreach (var (uid, next) in toReplace)
        {
            if (ReplaceEntity(uid, next))
                upgraded++;
        }

        return upgraded;
    }

    private int ApplyStructureUpgradeOnEntity(Entity<ShipyardServiceConsoleComponent> console, EntityUid uid, bool regular)
    {
        if (!TryGetStructureUpgrade(console, uid, regular, out var next))
            return 0;

        return ReplaceEntity(uid, next) ? 1 : 0;
    }

    private bool TryGetStructureUpgrade(
        Entity<ShipyardServiceConsoleComponent> console,
        EntityUid uid,
        bool regular,
        out EntProtoId next)
    {
        next = default;
        if (TerminatingOrDeleted(uid))
            return false;

        var proto = MetaData(uid).EntityPrototype?.ID;
        if (proto == null)
            return false;

        var map = regular ? console.Comp.RegularUpgrades : console.Comp.ReinforcedUpgrades;
        if (map.TryGetValue(proto, out next))
            return true;

        var diagonal = _tags.HasTag(uid, DiagonalTag) || proto.Contains("Diagonal", StringComparison.Ordinal);
        if (regular)
        {
            if (_tags.HasTag(uid, WallT1Tag) && !_tags.HasTag(uid, WallT2Tag) && !_tags.HasTag(uid, WallT3Tag))
            {
                next = diagonal ? "WallReinforcedDiagonal" : "WallReinforced";
                return next != proto;
            }

            return false;
        }

        if (_tags.HasTag(uid, WallT2Tag) && !_tags.HasTag(uid, WallT3Tag))
        {
            next = diagonal ? "WallPlastitaniumDiagonal" : "WallPlastitanium";
            return next != proto;
        }

        return false;
    }

    private bool ReplaceEntity(EntityUid uid, EntProtoId next)
    {
        var xform = Transform(uid);
        var coords = xform.Coordinates;
        var rotation = xform.LocalRotation;
        var anchored = xform.Anchored;
        var grid = xform.GridUid;

        var spawned = Spawn(next, coords);
        var spawnedXform = Transform(spawned);
        _transform.SetLocalRotation(spawnedXform, rotation);
        if (anchored)
            _transform.AnchorEntity(spawned, spawnedXform);

        if (grid != null)
            _shipRepair.RetargetSnapshotEntity(grid.Value, uid, spawned);

        QueueDel(uid);
        return true;
    }

    #endregion

    #region Damage cooldown

    private void OnRepairableDamaged(Entity<ShipRepairableComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        var grid = Transform(ent).GridUid;
        if (grid == null || !HasComp<ShuttleComponent>(grid.Value) && !HasComp<VesselComponent>(grid.Value))
            return;

        var damage = EnsureComp<ShipyardShuttleDamageComponent>(grid.Value);
        damage.LastDamageTime = _timing.CurTime;
        Dirty(grid.Value, damage);
    }

    private TimeSpan GetRepairCooldownEnd(Entity<ShipyardServiceConsoleComponent> console, EntityUid shuttle)
    {
        if (!TryComp<ShipyardShuttleDamageComponent>(shuttle, out var damage))
            return TimeSpan.Zero;

        return damage.LastDamageTime + console.Comp.RepairCooldown;
    }

    #endregion

    #region Helpers

    private EntityUid? GetShuttleFromDock(Entity<ShipyardDockComponent> dock, DockEvent args)
    {
        var ourGrid = Transform(dock).GridUid;
        if (args.GridAUid != ourGrid && args.GridBUid != ourGrid)
            return null;

        var shuttle = args.GridAUid == ourGrid ? args.GridBUid : args.GridAUid;
        return HasComp<MapGridComponent>(shuttle) ? shuttle : null;
    }

    private EntityUid? GetDockedShuttle(EntityUid dock, DockingComponent docking)
    {
        if (docking.DockedWith is not { } other)
            return null;

        var ourGrid = Transform(dock).GridUid;
        var otherGrid = Transform(other).GridUid;
        if (ourGrid == null || otherGrid == null || ourGrid == otherGrid)
            return null;

        return otherGrid;
    }

    private (int Fee, string Name) GetDockingFee(EntityUid shuttle, float feePercent)
    {
        GetVesselInfo(shuttle, out var name, out var price, out _, out _);
        var fee = Math.Max(0, (int) Math.Round(price * feePercent));
        return (fee, name);
    }

    private bool TryGetOccupancyFee(EntityUid shuttle, out int fee)
    {
        fee = 0;
        foreach (var dock in GetDocksForShuttle(shuttle))
        {
            if (dock.Comp.OccupancyCharged)
                return false;

            fee = dock.Comp.CachedFee;
            if (fee <= 0)
                (fee, _) = GetDockingFee(shuttle, dock.Comp.FeePercent);

            return fee > 0;
        }

        return false;
    }

    private void MarkOccupancyCharged(EntityUid shuttle)
    {
        foreach (var dock in GetDocksForShuttle(shuttle))
        {
            dock.Comp.OccupancyCharged = true;
            Dirty(dock);
        }
    }

    private IEnumerable<Entity<ShipyardDockComponent>> GetDocksForShuttle(EntityUid shuttle)
    {
        var query = EntityQueryEnumerator<ShipyardDockComponent, DockingComponent>();
        while (query.MoveNext(out var uid, out var dock, out var docking))
        {
            if (GetDockedShuttle(uid, docking) == shuttle)
                yield return (uid, dock);
        }
    }

    private void GetVesselInfo(
        EntityUid shuttle,
        out string name,
        out int price,
        out List<VesselClass> classes,
        out string classLabel)
    {
        name = MetaData(shuttle).EntityName;
        price = 0;
        classes = new List<VesselClass>();
        classLabel = Loc.GetString("shipyard-service-class-unknown");

        if (TryComp<VesselComponent>(shuttle, out var vessel) &&
            _prototypes.TryIndex(vessel.VesselId, out VesselPrototype? proto))
        {
            price = proto.Price;
            classes = proto.Classes;
            if (string.IsNullOrWhiteSpace(name))
                name = proto.Name;
            if (classes.Count > 0)
                classLabel = string.Join(", ", classes);
        }
        else
        {
            price = (int) _pricing.AppraiseGrid(shuttle);
        }

        if (TryComp<ShuttleDeedComponent>(shuttle, out var deed))
        {
            var full = deed.ShuttleName ?? name;
            if (!string.IsNullOrWhiteSpace(deed.ShuttleNameSuffix))
                full = $"{full} {deed.ShuttleNameSuffix}";
            name = full;
        }
    }

    private List<ShipyardServiceShuttleEntry> GetDockedShuttles(EntityUid console)
    {
        var result = new List<ShipyardServiceShuttleEntry>();
        var seen = new HashSet<EntityUid>();
        foreach (var dock in GetShipyardDocksForConsole(console))
        {
            if (!TryComp<DockingComponent>(dock, out var docking))
                continue;

            var shuttle = GetDockedShuttle(dock, docking);
            if (shuttle == null || !seen.Add(shuttle.Value))
                continue;

            GetVesselInfo(shuttle.Value, out var name, out _, out _, out var classLabel);
            result.Add(new ShipyardServiceShuttleEntry
            {
                Shuttle = GetNetEntity(shuttle.Value),
                Name = name,
                ClassLabel = classLabel
            });
        }

        return result;
    }

    private bool TryGetSelectedShuttle(Entity<ShipyardServiceConsoleComponent> ent, out EntityUid shuttle)
    {
        shuttle = default;
        if (ent.Comp.SelectedShuttle is not { } selected || TerminatingOrDeleted(selected))
            return false;

        if (!IsDockedShuttle(ent, selected))
            return false;

        shuttle = selected;
        return true;
    }

    private bool IsDockedShuttle(EntityUid console, EntityUid shuttle)
    {
        foreach (var dock in GetShipyardDocksForConsole(console))
        {
            if (!TryComp<DockingComponent>(dock, out var docking))
                continue;

            if (GetDockedShuttle(dock, docking) == shuttle)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Finds drydock airlocks that belong to the same grid or station as the console.
    /// </summary>
    private List<Entity<ShipyardDockComponent>> GetShipyardDocksForConsole(EntityUid console)
    {
        var list = new List<Entity<ShipyardDockComponent>>();
        var consoleGrid = Transform(console).GridUid;
        var consoleStation = _station.GetOwningStation(console);

        var query = EntityQueryEnumerator<ShipyardDockComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var dock, out var xform))
        {
            if (consoleGrid != null && xform.GridUid == consoleGrid)
            {
                list.Add((uid, dock));
                continue;
            }

            if (consoleStation != null && _station.GetOwningStation(uid) == consoleStation)
                list.Add((uid, dock));
        }

        return list;
    }

    private List<EntityUid> GetGridChildren(EntityUid grid)
    {
        var list = new List<EntityUid>();
        var enumerator = Transform(grid).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (TerminatingOrDeleted(child))
                continue;

            list.Add(child);
        }

        return list;
    }

    #endregion
}
