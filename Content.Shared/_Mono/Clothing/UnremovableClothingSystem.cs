using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Interaction;
using Content.Shared.Whitelist;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared.Clothing.EntitySystems;

/// <summary>
/// A system that handles toggleable unremoveable clothing.
/// </summary>
public sealed class UnremovableClothingSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UnremovableClothingComponent, BeingUnequippedAttemptEvent>(OnUnequip);
        SubscribeLocalEvent<UnremovableClothingComponent, ExaminedEvent>(OnUnequipMarkup);
        SubscribeLocalEvent<UnremovableClothingRemoverComponent, AfterInteractEvent>(OnInteract);
    }

    private void OnUnequip(Entity<UnremovableClothingComponent> unremovableClothing, ref BeingUnequippedAttemptEvent args)
    {
        if (TryComp<ClothingComponent>(unremovableClothing, out var clothing) && (clothing.Slots & args.SlotFlags) == SlotFlags.NONE)
            return;

        if (unremovableClothing.Comp.IsUnremovable)
        {
            args.Cancel();
        }
    }

    private void OnInteract(EntityUid uid, UnremovableClothingRemoverComponent component, ref AfterInteractEvent eventArgs)
    {

        if (eventArgs.Handled)
            return;

        // standard interaction checks
        if (!eventArgs.CanReach)
            return;

        if (eventArgs.Target is not { } targetUid)
            return;

        HandleRemovability(targetUid, component, ref eventArgs);
        if (eventArgs.Handled)
            return;

        // if not found, check the entity's inventory (if it exists) for an entity with the component. Return once one is found.
        // This iterates once, so it won't check nested inventories.
        if (_inventory.TryGetInventoryEntity<UnremovableClothingComponent>(targetUid, out var equippedTargetUid))
            HandleRemovability(equippedTargetUid, component, ref eventArgs);
    }

    private void HandleRemovability(EntityUid targetUid, UnremovableClothingRemoverComponent component, ref AfterInteractEvent eventArgs)
    {
        if (!TryComp<UnremovableClothingComponent>(targetUid, out var clothing))
            return;

        // if whitelist is null or passes, continue
        if (!_whitelistSystem.IsWhitelistPassOrNull(component.Whitelist, targetUid))
            return;

        // toggle unremoveability
        if (clothing.IsUnremovable)
        {
            clothing.IsUnremovable = false;
            _popup.PopupPredicted(Loc.GetString("comp-unremovable-clothing-disabled", ("target", targetUid)), eventArgs.User, targetUid);
        }
        else
        {
            clothing.IsUnremovable = true;
            _popup.PopupPredicted(Loc.GetString("comp-unremovable-clothing-enabled", ("target", targetUid)), eventArgs.User, targetUid);
        }

        // update client
        Dirty(targetUid, clothing);
        eventArgs.Handled = true;
        return;
    }

    private void OnUnequipMarkup(Entity<UnremovableClothingComponent> unremovableClothing, ref ExaminedEvent args)
    {
        if (unremovableClothing.Comp.IsUnremovable)
            args.PushMarkup(Loc.GetString("comp-unremovable-clothing"));
    }
}
