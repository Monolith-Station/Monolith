using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Clothing.EntitySystems;

/// <summary>
///     A system for the operation of a component that prohibits the removal of an item with that component.
/// </summary>
public sealed class UnremovableClothingSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
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
        if (!_net.IsServer)
            return;

        if (eventArgs.Handled)
            return;

        // standard interaction checks
        if (!eventArgs.CanReach)
            return;

        // behaviour will depends on target type
        if (eventArgs.Target != null)
        {
            var targetUid = (EntityUid)eventArgs.Target;

            // replace broken light in fixture?

            HandleRemovability(targetUid, ref eventArgs);
            if (eventArgs.Handled)
                return;

            if (TryComp<InventoryComponent>(targetUid, out var inventory))
            {
                foreach (var container in inventory.Containers)
                {
                    foreach (var entity in container.ContainedEntities)
                    {
                        HandleRemovability(entity, ref eventArgs);
                        if (eventArgs.Handled)
                            return;
                    }
                }
            }
        }
    }

    private void HandleRemovability(EntityUid targetUid, ref AfterInteractEvent eventArgs)
    {
        if (TryComp<UnremovableClothingComponent>(targetUid, out var clothing))
        {
            switch (clothing.IsUnremovable)
            {
                case true:
                    clothing.IsUnremovable = false;
                    _popup.PopupEntity(Loc.GetString("comp-unremovable-clothing-disabled", ("target", targetUid)), targetUid);
                    break;
                case false:
                    clothing.IsUnremovable = true;
                    _popup.PopupEntity(Loc.GetString("comp-unremovable-clothing-enabled", ("target", targetUid)), targetUid);
                    break;
            }
            Dirty(targetUid, clothing);

            eventArgs.Handled = true;
            return;
        }
    }

    private void OnUnequipMarkup(Entity<UnremovableClothingComponent> unremovableClothing, ref ExaminedEvent args)
    {
        if (unremovableClothing.Comp.IsUnremovable)
            args.PushMarkup(Loc.GetString("comp-unremovable-clothing"));
    }
}
