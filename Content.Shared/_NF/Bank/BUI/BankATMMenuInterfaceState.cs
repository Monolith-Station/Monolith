using Robust.Shared.Serialization;

namespace Content.Shared._NF.Bank.BUI;

[NetSerializable, Serializable]
public sealed class BankATMMenuInterfaceState : BoundUserInterfaceState
{
    /// <summary>
    /// bank balance of the character using the atm
    /// </summary>
    public int Balance;

    /// <summary>
    /// are the buttons enabled
    /// </summary>
    public bool Enabled;

    /// <summary>
    /// how much cash is inserted
    /// </summary>
    public int Deposit;

    /// <summary>
    /// how much cash is inserted - without tax calculations - Mono change
    /// </summary>
    public int DepositUntaxed;

    public BankATMMenuInterfaceState(int balance, bool enabled, int deposit, int depositUntaxed)
    {
        Balance = balance;
        Enabled = enabled;
        Deposit = deposit;
        DepositUntaxed = depositUntaxed;
    }
}
