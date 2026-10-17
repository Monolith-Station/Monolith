using System.Numerics;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Clothing.Components;
using Content.Shared.Coordinates;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.UserInterface;
using Content.Shared.Wall;
using Robust.Shared.Audio.Systems;
using Content.Shared.Destructible;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Storage;

namespace Content.Shared._RMC14.Vendors;

public abstract class SharedCMAutomatedVendorSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ThrowingSystem _throwingSystem = default!;

    // TODO RMC14 make this a prototype
    public const string SpecialistPoints = "Specialist";

    private readonly Dictionary<EntProtoId, CMVendorEntry> _entries = new();
    private readonly List<CMVendorEntry> _boxEntries = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMAutomatedVendorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMAutomatedVendorComponent, ActivatableUIOpenAttemptEvent>(OnUIOpenAttempt);
        SubscribeLocalEvent<CMAutomatedVendorComponent, DestructionEventArgs>(OnVendorDestruction);

        SubscribeLocalEvent<RMCRecentlyVendedComponent, GotEquippedHandEvent>(OnRecentlyGotEquipped);
        SubscribeLocalEvent<RMCRecentlyVendedComponent, GotEquippedEvent>(OnRecentlyGotEquipped);


        Subs.BuiEvents<CMAutomatedVendorComponent>(CMAutomatedVendorUI.Key,
            subs =>
            {
                subs.Event<CMVendorVendBuiMsg>(OnVendBui);
            });
    }

    private void OnMapInit(Entity<CMAutomatedVendorComponent> ent, ref MapInitEvent args)
    {
        var transform = Transform(ent.Owner);
        _entries.Clear();
        _boxEntries.Clear();
        foreach (var section in ent.Comp.Sections)
        {
            foreach (var entry in section.Entries)
            {
                _entries.TryAdd(entry.Id, entry);
                if (entry.Box != null)
                {
                    _boxEntries.Add(entry);
                    continue;
                }

                entry.Multiplier = entry.Amount;
                entry.Max = entry.Amount;

            }
        }

        foreach (var boxEntry in _boxEntries)
        {
            if (boxEntry.Box is not { } box)
                continue;

            if (_entries.TryGetValue(box, out var entry))
                AmountUpdated(ent, entry);
        }

        Dirty(ent);
    }

    private void OnUIOpenAttempt(Entity<CMAutomatedVendorComponent> vendor, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (TryComp<CMVendorUserComponent>(args.User, out var vendorUser) &&
            TryComp<RMCVendorUserRechargeComponent>(args.User, out var recharge))
        {
            var ticks = (_timing.CurTime - recharge.LastUpdate) / recharge.TimePerUpdate;
            var points = (int)Math.Floor(ticks * recharge.PointsPerUpdate);
            if (points > 0)
            {
                vendorUser.Points = Math.Min(recharge.MaxPoints, vendorUser.Points + points);
                recharge.LastUpdate = _timing.CurTime;
                DirtyEntity(args.User);
            }
        }

        if (HasComp<BypassInteractionChecksComponent>(args.User))
            return;

        if (vendor.Comp.Hacked)
            return;
    }

    private void OnVendorDestruction(Entity<CMAutomatedVendorComponent> vendor, ref DestructionEventArgs args)
    {
        if (vendor.Comp.EjectContentsOnDestruction)
            EjectAllVendorContents(vendor);
    }

    private void EjectAllVendorContents(Entity<CMAutomatedVendorComponent> vendor)
    {
        // Get all available items with their quantity
        var inventory = GetAvailableInventoryWithAmounts(vendor.Comp);

        foreach (var (itemId, amount) in inventory)
        {
            // Create items in quantity amount
            for (int i = 0; i < amount; i++)
            {
                // Create item near the vendor
                var coords = Transform(vendor).Coordinates;
                var spawnedItem = Spawn(itemId, coords);

                // Throw in a random direction with a random force
                var direction = new Vector2(_random.NextFloat(-1, 1), _random.NextFloat(-1, 1));
                var throwForce = _random.NextFloat(1f, 7f);
                _throwingSystem.TryThrow(spawnedItem, direction, throwForce);
            }
        }
    }

    private List<(EntProtoId Id, int Amount)> GetAvailableInventoryWithAmounts(CMAutomatedVendorComponent component)
    {
        var inventory = new List<(EntProtoId Id, int Amount)>();

        foreach (var section in component.Sections)
        {
            foreach (var entry in section.Entries)
            {
                if (entry.Amount > 0)
                {
                    inventory.Add((entry.Id, entry.Amount.Value));
                }
            }
        }

        return inventory;
    }

    private void OnRecentlyGotEquipped<T>(Entity<RMCRecentlyVendedComponent> ent, ref T args)
    {
        RemCompDeferred<WallMountComponent>(ent);
    }

    protected virtual void OnVendBui(Entity<CMAutomatedVendorComponent> vendor, ref CMVendorVendBuiMsg args)
    {
        _audio.PlayPredicted(vendor.Comp.Sound, vendor, args.Actor);

        if (_net.IsClient)
            return;

        var comp = vendor.Comp;
        var sections = comp.Sections.Count;
        var actor = args.Actor;
        if (args.Section < 0 || args.Section >= sections)
        {
            Log.Error($"{ToPrettyString(actor)} sent an invalid vend section: {args.Section}. Max: {sections}");
            return;
        }

        var section = comp.Sections[args.Section];
        var entries = section.Entries.Count;
        if (args.Entry < 0 || args.Entry >= entries)
        {
            Log.Error($"{ToPrettyString(actor)} sent an invalid vend entry: {args.Entry}. Max: {entries}");
            return;
        }

        var entry = section.Entries[args.Entry];
        if (entry.Amount is <= 0)
            return;

        if (!_prototypes.TryIndex(entry.Id, out var entity))
        {
            Log.Error($"Tried to vend non-existent entity: {entry.Id}");
            return;
        }

        var user = CompOrNull<CMVendorUserComponent>(actor);
        if (section.TakeAll is { } takeAll)
        {
            user = EnsureComp<CMVendorUserComponent>(actor);
            if (!user.TakeAll.Add((takeAll, entry.Id)))
            {
                Log.Error($"{ToPrettyString(actor)} tried to buy too many take-alls.");
                return;
            }

            Dirty(actor, user);
        }

        if (section.TakeOne is { } takeOne)
        {
            user = EnsureComp<CMVendorUserComponent>(actor);
            if (!user.TakeOne.Add(takeOne))
            {
                Log.Error($"{ToPrettyString(actor)} tried to buy too many take-ones.");
                return;
            }

            Dirty(actor, user);
        }

        if (section.Choices is { } choices)
        {
            user = EnsureComp<CMVendorUserComponent>(actor);
            if (!user.Choices.TryGetValue(choices.Id, out var playerChoices))
            {
                playerChoices = 0;
                user.Choices[choices.Id] = playerChoices;
                Dirty(actor, user);
            }

            if (playerChoices >= choices.Amount)
            {
                Log.Error($"{ToPrettyString(actor)} tried to buy too many choices.");
                return;
            }

            user.Choices[choices.Id] = ++playerChoices;
            Dirty(actor, user);
        }

        void ResetChoices()
        {
            if (section.Choices is { } choices && user != null)
                user.Choices[choices.Id]--;
            if (section.TakeOne is { } takeOne && user != null)
                user.TakeOne.Remove(takeOne);
        }

        if (section.SharedSpecLimit is { } globalLimit && !HasComp<IgnoreSpecLimitsComponent>(actor))
        {
            if (TryComp<RMCVendorSpecialistComponent>(vendor, out var thisSpecVendor))
            {
                // If the vendor's own value is at or above the capacity, immediately return.
                if (thisSpecVendor.GlobalSharedVends.TryGetValue(args.Entry, out var vendCount) &&
                    vendCount >= globalLimit)
                {
                    // FIXME
                    ResetChoices();
                    _popup.PopupEntity(Loc.GetString("cm-vending-machine-specialist-max"), vendor, actor);
                    return;
                }

                // Get every RMCVendorSpec
                var specVendors = EntityQueryEnumerator<RMCVendorSpecialistComponent>();
                var allVendorsTotal = 0;

                // Goes through each RMCVendorSpec and gets the value for this kit type.
                while (specVendors.MoveNext(out _, out var specVendorComponent))
                {
                    foreach (var linkedEntry in args.LinkedEntries)
                    {
                        specVendorComponent.GlobalSharedVends.TryGetValue(linkedEntry, out var linkedCount);
                        allVendorsTotal += linkedCount;
                    }
                    if (specVendorComponent.GlobalSharedVends.TryGetValue(args.Entry, out vendCount))
                    {
                        allVendorsTotal += vendCount;
                    }
                }

                if (allVendorsTotal >= globalLimit)
                {
                    ResetChoices();
                    _popup.PopupEntity(Loc.GetString("cm-vending-machine-specialist-max"), vendor.Owner, actor);
                    return;
                }

                var old = thisSpecVendor.GlobalSharedVends.GetValueOrDefault(args.Entry, 0);
                thisSpecVendor.GlobalSharedVends[args.Entry] = old + 1;
                Dirty(vendor, thisSpecVendor);

                AddComp(actor, new RMCSpecCryoRefundComponent
                {
                    Vendor = vendor,
                    Entry = args.Entry
                }, true);
            }
        }

        if (entry.Points != null)
        {
            if (user == null)
            {
                Log.Error(
                    $"{ToPrettyString(actor)} tried to buy {entry.Id} for {entry.Points} points without having points.");
                return;
            }

            var userPoints = vendor.Comp.PointsType == null
                ? user.Points
                : user.ExtraPoints?.GetValueOrDefault(vendor.Comp.PointsType) ?? 0;
            if (userPoints < entry.Points)
            {
                Log.Error(
                    $"{ToPrettyString(actor)} with {user.Points} tried to buy {entry.Id} for {entry.Points} points without having enough points.");
                return;
            }

            if (vendor.Comp.PointsType == null)
                user.Points -= entry.Points.Value;
            else if (user.ExtraPoints != null)
                user.ExtraPoints[vendor.Comp.PointsType] = userPoints - (entry.Points ?? 0);

            Dirty(actor, user);
        }

        if (entry.Amount != null)
        {
            if (entry.Box is { } box)
            {
                var foundEntry = false;
                foreach (var vendorSection in vendor.Comp.Sections)
                {
                    foreach (var vendorEntry in vendorSection.Entries)
                    {
                        if (vendorEntry.Id != box)
                            continue;
                        Dirty(vendor);
                        AmountUpdated(vendor, vendorEntry);
                        foundEntry = true;
                        break;
                    }

                    if (foundEntry)
                        break;
                }
            }
            else
            {
                entry.Amount--;
                Dirty(vendor);
                AmountUpdated(vendor, entry);
            }
        }
        var min = comp.MinOffset;
        var max = comp.MaxOffset;
        for (var i = 0; i < entry.Spawn; i++)
        {
            var offset = _random.NextVector2Box(min.X, min.Y, max.X, max.Y);
            if (entity.TryGetComponent(out CMVendorBundleComponent? bundle, _compFactory))
            {
                foreach (var bundled in bundle.Bundle)
                {
                    Vend(vendor, actor, bundled, offset, entry.ReplaceSlot);
                }
            }
            else
            {
                Vend(vendor, actor, entry.Id, offset, entry.ReplaceSlot);
            }
        }

        if (entity.TryGetComponent(out CMChangeUserOnVendComponent? change, _compFactory) &&
            change.AddComponents != null)
        {
            EntityManager.AddComponents(actor, change.AddComponents);
        }
    }

    private void Vend(EntityUid vendor, EntityUid player, EntProtoId toVend, Vector2 offset, SlotFlags? replaceSlot = null)
    {

            var spawn = SpawnNextToOrDrop(toVend, vendor);
            AfterVend(spawn, player, vendor, offset, replaceSlot: replaceSlot);
    }

    private void AfterVend(EntityUid spawn, EntityUid player, EntityUid vendor, Vector2 offset, bool vended = false, SlotFlags? replaceSlot = null)
    {
        var recently = EnsureComp<RMCRecentlyVendedComponent>(spawn);

        Dirty(spawn, recently);

        var mount = EnsureComp<WallMountComponent>(spawn);
        mount.Arc = Angle.FromDegrees(360);
        Dirty(spawn, mount);

        if (!vended)
        {
            var grabbed = Grab(player, spawn, replaceSlot);
            if (!grabbed && TryComp(spawn, out TransformComponent? xform))
                _transform.SetLocalPosition(spawn, xform.LocalPosition + offset, xform);
        }

        var ev = new RMCAutomatedVendedUserEvent(spawn);
        RaiseLocalEvent(player, ref ev);
    }

    private bool Grab(EntityUid player, EntityUid item, SlotFlags? replaceSlot = null)
    {
        if (!HasComp<ItemComponent>(item))
            return false;

        if (TryAttachWebbing(player, item))
            return true;

        if (!TryComp(item, out ClothingComponent? clothing))
            return _hands.TryPickupAnyHand(player, item);

        if (replaceSlot != null)
        {
            EntityUid? itemToReplace = null;

            var slots = _inventory.GetSlotEnumerator(player, replaceSlot.Value);
            while (slots.MoveNext(out var slot))
            {
                if (slot.ContainedEntity != null)
                {
                    itemToReplace = slot.ContainedEntity;
                    _inventory.TryUnequip(player, slot.ID, true);
                    break;
                }
            }

            if (itemToReplace != null)
            {
                if (HasComp<StorageComponent>(item) && HasComp<StorageComponent>(itemToReplace))
                    _storage.TransferEntities(itemToReplace.Value, item);
            }
        }
        return _hands.TryPickupAnyHand(player, item);
    }

    private bool TryAttachWebbing(EntityUid player, EntityUid item)
    {
        return false;
    }

    public void SetPoints(Entity<CMVendorUserComponent> user, int points)
    {
        user.Comp.Points = points;
        Dirty(user);
    }

    public void SetExtraPoints(Entity<CMVendorUserComponent> user, string key, int points)
    {
        user.Comp.ExtraPoints ??= new Dictionary<string, int>();
        user.Comp.ExtraPoints[key] = points;
        Dirty(user);
    }

    public void AmountUpdated(Entity<CMAutomatedVendorComponent> vendor, CMVendorEntry entry)
    {
        foreach (var section in vendor.Comp.Sections)
        {
            if (!section.HasBoxes)
                continue;

            foreach (var sectionEntry in section.Entries)
            {
                if (sectionEntry.Box is not { } box)
                    continue;

                if (entry.Id != box)
                    continue;
            }
        }
    }
}
