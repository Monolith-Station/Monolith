using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._NF.Bank.Components;

[RegisterComponent, NetworkedComponent]

public sealed partial class BankATMComponent : Component
{
    [DataField]
    public ProtoId<StackPrototype> PrimaryCashType = "Credit";

    [DataField]
    public List<ProtoId<StackPrototype>> CashTypes = new() { "Credit", "FactionCredit" };

    public static string CashSlotId = "bank-ATM-cashSlot";

    // A dictionary of the accounts to credit, and fractions to remove from each deposit.
    [DataField]
    public Dictionary<SectorBankAccount, float> TaxAccounts = new();

    [DataField]
    public ItemSlot CashSlot = new();

    [DataField("soundError")]
    public SoundSpecifier ErrorSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField("soundConfirm")]
    public SoundSpecifier ConfirmSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");
}
