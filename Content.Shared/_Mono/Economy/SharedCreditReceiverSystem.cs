using Content.Shared._Mono.Economy.Component;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Shared.Containers;

namespace Content.Shared._Mono.Economy;

public abstract partial class SharedCreditReceiverSystem : EntitySystem
{
    [Dependency] private readonly SharedStackSystem _stack = default!; // Frontier
    [Dependency] protected readonly ItemSlotsSystem ItemSlots = default!; // Frontier

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CreditReceiverComponent, EntInsertedIntoContainerMessage>(OnEntityInserted); // Frontier // Mono - Seperation of Cash from VendingMachineComp
        SubscribeLocalEvent<CreditReceiverComponent, EntRemovedFromContainerMessage>(OnEntityRemoved); // Frontier // Mono - Seperation of Cash from VendingMachineComp

    }

    // Frontier: cash slot logic
    /// <remarks> Mono - This was seperated out from <see cref="VendingMachines"/> into its own system, since we'd like Ironman players to use shipyard consoles</remarks>>
    private void OnEntityInserted(Entity<CreditReceiverComponent> ent, ref EntInsertedIntoContainerMessage args) // Mono - Seperation of Cash from VendingMachineComp
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

    private void OnEntityRemoved(Entity<CreditReceiverComponent> ent, ref EntRemovedFromContainerMessage args) // Mono
    {
        ent.Comp.CashSlotBalance = 0;
        Dirty(ent, ent.Comp);
    }
}
