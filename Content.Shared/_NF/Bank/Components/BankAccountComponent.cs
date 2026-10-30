using Robust.Shared.GameStates;

namespace Content.Shared._NF.Bank.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class BankAccountComponent : Component
{
    private int _balance = 0; // Mono: get around CS9266

    // The amount of money this entity has in their bank account.
    // Should not be modified directly, may be out-of-date.
    [DataField, Access(typeof(SharedBankSystem))]
    [AutoNetworkedField]
    public int Balance
    {
        get { return _balance; } // Mono: remove persistent economy by forcing balance to $0.
        set { _balance = 0; }
    }
}
