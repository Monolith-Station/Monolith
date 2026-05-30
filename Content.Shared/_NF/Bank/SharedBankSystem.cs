using Content.Shared._NF.Bank.Components;
using Content.Shared._Mono.CCVar;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._NF.Bank;

[NetSerializable, Serializable]
public enum BankATMMenuUiKey : byte
{
    ATM,
    BlackMarket
}

public abstract partial class SharedBankSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ItemSlotsSystem _itemSlotsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BankATMComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<BankATMComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<StationBankATMComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<StationBankATMComponent, ComponentRemove>(OnComponentRemove);
    }

    private void OnComponentInit(EntityUid uid, BankATMComponent component, ComponentInit args)
    {
        _itemSlotsSystem.AddItemSlot(uid, BankATMComponent.CashSlotId, component.CashSlot);
    }

    private void OnComponentRemove(EntityUid uid, BankATMComponent component, ComponentRemove args)
    {
        _itemSlotsSystem.RemoveItemSlot(uid, component.CashSlot);
    }

    private void OnComponentInit(EntityUid uid, StationBankATMComponent component, ComponentInit args)
    {
        _itemSlotsSystem.AddItemSlot(uid, StationBankATMComponent.CashSlotId, component.CashSlot);
    }

    private void OnComponentRemove(EntityUid uid, StationBankATMComponent component, ComponentRemove args)
    {
        _itemSlotsSystem.RemoveItemSlot(uid, component.CashSlot);
    }

    public void GetTaxedDepositAmount(int deposit, int balance, out int amount, out int taxedAway)
    {
        double threshold = _cfg.GetCVar(MonoCVars.DepositThreshold); // Default is 1000000
        double high_exp = _cfg.GetCVar(MonoCVars.DepositHighExp); // Default is 2

        double deposit_low = Math.Max(Math.Min(deposit, threshold - balance), 0);
        double deposit_high = Math.Max(0, deposit + Math.Min(balance - threshold, 0));
        double bank_high = Math.Max(balance, threshold);
        double adj_exp = high_exp + 1f;
        var taxedDeposit = 0;
        if (deposit >= 1)
        {
            taxedDeposit = (int)Math.Round(deposit_low + Math.Pow(Math.Pow(bank_high, adj_exp) + deposit_high * adj_exp * Math.Pow(threshold, high_exp), 1f / adj_exp) - bank_high);
        }
        else
        {
            taxedDeposit = 0;
        }
        amount = taxedDeposit;
        taxedAway = deposit - amount;
        return;
    }
}

