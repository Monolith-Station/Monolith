using Robust.Shared.GameStates;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._Mono.Economy.Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedCreditReceiverSystem))]
public sealed partial class CreditReceiverComponent : Robust.Shared.GameObjects.Component
{
    // Item slot for cash
    [DataField]
    public ItemSlot? CashSlot = null;

    /// <summary>
    /// Name of the cash slot, if there is one.  Null if there isn't.
    /// </summary>
    [DataField]
    public string? CashSlotName;

    /// <summary>
    /// The type of currency to accept in the item slot.
    /// </summary>
    [DataField]
    public string? CurrencyStackType;

    /// <summary>
    /// The current balance in the cash slot.
    /// Kept for
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CashSlotBalance;
}
