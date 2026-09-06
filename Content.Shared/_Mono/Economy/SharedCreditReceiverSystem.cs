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
    private readonly ISawmill _log = default!;
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

    /// <summary>
    /// Simples way of interaction. If there's cash in a creditReceiver, you get the count.
    /// </summary>
    /// <param name="uid"></param>
    /// <returns>0 if there's no money or incompatible, an int otherwise.</returns>
    /// <remarks>For building more complex logic, try using <see cref="TryGetCash"/> or <see cref="TryGetCashBalance"/> instead.</remarks>
    public int GetCashBalance(EntityUid uid)
    {
        return TryGetCashBalance(uid, out var balance) ? (int)balance : 0;
    }


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
    /// <remarks>When you don't care about the boolean and just care about the int, try using <see cref="GetCashBalance"/> instead.</remarks>
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

    // Try to implement ItemSlots.TryGetSlot(uid, creditComponent.CashSlotName, out var cashSlot) && TryComp<StackComponent>(cashSlot?.ContainerSlot?.ContainedEntity, out var stackComp) && stackComp!.StackTypeId == creditComponent.CurrencyStackType
    /// <summary>
    /// Method to grab the currency in a CreditReceiver, as specified in the components slot
    /// </summary>
    /// <param name="uid">The CreditReceiver entity, like a Vending Machine</param>
    /// <param name="stack">Null or EntityUID/StackComponent tuple, like a stack of spesos</param>
    /// <returns>True if successfully retrieved the contained currency, false if else.</returns>
    /// <remarks>When you don't care about the boolean and just care about the int, try using <see cref="GetCashBalance"/> instead.</remarks>
    public bool TryGetCash(EntityUid uid, [NotNullWhen(true)] out Entity<StackComponent>? stack, [NotNullWhen(true)] out int balance)
    {
        stack = null;
        balance = 0;

        // We dont accept cash here, buddy.
        if (!TryComp<CreditReceiverComponent>(uid, out var receiver))
            return false;

        // In if in yaml a parent has CreditReceiver but doesn't actually want cash behavior, set these to null.
        if (receiver.CashSlotName == null)
            return false;

        // If there's no money in the bag, we fail to return anything.
        if (!TryGetCashSlot(uid, out var cashSlot)
            || cashSlot.ContainerSlot == null)
            return false;

        // This whole system assumes the currency item is a stack of items.
        if ( cashSlot.ContainerSlot.ContainedEntity == null
            || !TryComp<StackComponent>(cashSlot.ContainerSlot.ContainedEntity, out var stackComp))
            return false;

        // Sanity check that the stack item is of the same type as specified in the CreditReceiverComponent.CurrencyStackType
        if (stackComp.StackTypeId != receiver.CurrencyStackType)
            return false;

        stack = cashSlot.ContainerSlot.ContainedEntity!; // I am double and triple sure there are no nulls when we get here.
        balance = receiver.CashSlotBalance;
        return true;
    }

    /// <summary>
    /// Not sure why you would want this when <see cref="TryGetCash"/> exists
    /// </summary>
    public bool TryGetCashSlot(EntityUid uid, [NotNullWhen(true)] out ItemSlot? slot)
    {
        slot = null;

        if (!TryComp<CreditReceiverComponent>(uid, out var receiver))
            return false;

        if (receiver.CashSlot == null || receiver.CashSlotName == null)
            return false;

        if (!ItemSlots.TryGetSlot(uid, receiver.CashSlotName, out var cashSlot))
            return false;

        slot = cashSlot;
        return true;
    }

    /// <summary>
    /// Attempts to process cash payment, reduce the currency's stack and dirties the component.
    /// </summary>
    /// <param name="uid">UID of the machine.</param>
    /// <param name="amount">The amount to be deposited.</param>
    /// <param name="partialPaymentAllowed">Boolean to ask if we require the amount to be fully paid here.</param>
    /// <param name="remainingDebt">Returns any remaining currency that needs to be paid if partialPaymentAllowed is true, otherwise 0.</param>
    /// <returns>Whether the transaction was successfully or not</returns>
    /// <remarks>By default, will only return true if the full amount can be payed. If you want to use this in tandem with <paramref name="partialPaymentAllowed"/> so true/false depends on if there was no debt remains to be paid.</remarks>
    public bool TryCashPayment(EntityUid uid, int amount, out int remainingDebt, bool partialPaymentAllowed = false)
    {
        remainingDebt = 0;
        if (amount <= 0)
        {
            _log.Info($"TryCashPayment: {amount} is invalid from Uid {uid}");
            return false;
        }

        if (!TryComp<CreditReceiverComponent>(uid, out var receiver))
        {
            _log.Info($"TryCashPayment: {uid} has somehow got money stuck inside.");
            return false;
        }

        // Failed to find money
        if (!TryGetCash(uid, out var cash, out var balance))
            return false;

        // Broke spacer alert
        if (!partialPaymentAllowed && balance - amount < 0)
            return false;

        if (partialPaymentAllowed)
            remainingDebt = Math.Abs(amount - balance);

        var newCashSlotBalance = Math.Max(balance - amount, 0);

        // Finally, we adjust the currency and the component to complete the transaction.
        _stack.SetCount(cash.Value.Owner, newCashSlotBalance, cash.Value.Comp);
        receiver.CashSlotBalance = newCashSlotBalance;
        Dirty(uid, receiver);
        return true;
    }

    #endregion
}
