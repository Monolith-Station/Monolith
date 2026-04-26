using System.Diagnostics.CodeAnalysis;
using Content.Shared._Mono.Economy.Component;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Content.Shared.VendingMachines;
using Robust.Shared.Containers;

namespace Content.Shared._Mono.Economy;

/// <summary>
/// Many systems and methods ripped out of <see cref="SharedVendingMachineSystem"/> and moved here. To handle machines that can accept any sort of currency and provide something in return.
/// </summary>
/// <remarks>Mostly to be used in other systems that like to implement this behavior to avoid code duplication.</remarks>
public abstract partial class SharedCreditReceiverSystem : EntitySystem
{
    [Dependency] private readonly SharedStackSystem _stack = default!; // Frontier
    [Dependency] protected readonly ItemSlotsSystem ItemSlots = default!; // Frontier

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CreditReceiverComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CreditReceiverComponent, EntInsertedIntoContainerMessage>(OnEntityInserted);
        SubscribeLocalEvent<CreditReceiverComponent, EntRemovedFromContainerMessage>(OnEntityRemoved);
    }

    protected void OnMapInit(EntityUid uid, CreditReceiverComponent component, MapInitEvent args)
    {
        if (component.CashSlot != null && component.CashSlotName != null)
            ItemSlots.AddItemSlot(uid, component.CashSlotName, component.CashSlot);
    }


    protected void Update(Entity<CreditReceiverComponent> ent)
    {
        if (ent.Comp.CashSlotName != null
            && ent.Comp.CurrencyStackType != null
            && ItemSlots.TryGetSlot(ent, ent.Comp.CashSlotName, out var slot)
            && TryComp<StackComponent>(slot?.ContainerSlot?.ContainedEntity, out var stack)
            && stack.StackTypeId == ent.Comp.CurrencyStackType)
        {
            ent.Comp.CashSlotBalance = stack.Count;
        }
        else
        {
            ent.Comp.CashSlotBalance = 0;
        }
        Dirty(ent, ent.Comp);
    }

    // Frontier: cash slot logic
    /// <remarks> Mono - This was seperated out from <see cref="VendingMachines"/> into its own system, since we'd like Ironman players to use shipyard consoles</remarks>>
    protected void OnEntityInserted(Entity<CreditReceiverComponent> ent, ref EntInsertedIntoContainerMessage args) // Mono - Seperation of Cash from VendingMachineComp
    {
        Update(ent);
    }

    protected void OnEntityRemoved(Entity<CreditReceiverComponent> ent, ref EntRemovedFromContainerMessage args) // Mono
    {
        Update(ent);
    }

    #region API

    public bool CanPayWithCredit(Entity<CreditReceiverComponent> ent)
    {
        return TryComp<CreditReceiverComponent>(ent.Owner, out var creditComponent) && creditComponent.CashSlotName != null && creditComponent.CurrencyStackType != null;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="uid">EntityUID to be checked</param>
    /// <param name="amount">If true, stores here the amount of currency found.</param>
    /// <returns>Returns true if the entity has the CreditReceiverComponent and how much cash is stored. Returns false if the entity does not accept currency.</returns>
    public bool TryGetCashBalance(EntityUid uid, [NotNullWhen(true)] out int? amount)
    {
        amount = null;

        if (!TryComp<CreditReceiverComponent>(uid, out var receiver))
        {
            return false;
        }

        amount = receiver.CashSlotBalance;
        return true;
    }

    public bool TryGetCashSlot(EntityUid uid, [NotNullWhen(true)] out ItemSlot? slot)
    {
        slot = null;

        if (!TryComp<CreditReceiverComponent>(uid, out var receiver)) { return false; }

        if (receiver.CashSlot == null) { return false; }

        slot = receiver.CashSlot; // Fixme - Possible null reference
        return true;
    }

    public bool TryGetCashEntity(EntityUid uid, [NotNullWhen(true)] out Entity<CreditReceiverComponent>? cashEntity)
    {
        cashEntity = null;
        if (!TryComp<CreditReceiverComponent>(uid, out var receiver)) { return false; }

        if (!TryGetCashSlot(uid, out var slot)
            || slot == null)
            return false;

        if (!TryComp<StackComponent>(slot?.ContainerSlot?.ContainedEntity, out var stackComp)
            && stackComp!.StackTypeId == receiver.CurrencyStackType)
            return false;

        if (slot!.ContainerSlot == null) // Fixme - possible null reference
            return false;

        cashEntity = slot.ContainerSlot!.ContainedEntity!.Value!;
        return true;
    }

    #endregion
}
