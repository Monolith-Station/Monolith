using Robust.Shared.GameStates;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Economy.Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedCreditReceiverSystem))]
public sealed partial class CreditReceiverComponent : Robust.Shared.GameObjects.Component
{
    /// <summary>
    /// Item slot for cash.</summary>
    /// <remarks>
    /// Set this to Null if you want to disable this component
    /// in case you can't get rid of it, i.e. YAML inherited.
    /// </remarks>
    [DataField]
    public ItemSlot CashSlot;

    /// <summary>
    /// Name of the cash slot
    /// </summary>
    [DataField]
    public string CashSlotName = "cash_slot";

    /// <summary>
    /// The type of entity to be accepted in the item slot.
    /// </summary>
    /// <remarks> By default, it's standard spesos.</remarks>
    [DataField]
    public EntProtoId<StackComponent> CurrencyStackType = "Credit";

    /// <summary>
    /// The current balance in the cash slot.
    /// Kept for
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CashSlotBalance;
}
