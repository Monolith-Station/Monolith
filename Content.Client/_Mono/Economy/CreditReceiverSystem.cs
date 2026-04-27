using Content.Shared._Mono.Economy;
using Content.Shared._Mono.Economy.Component;
using Content.Shared.VendingMachines;

namespace Content.Client._Mono.Economy;

public sealed partial class CreditReceiverSystem : SharedCreditReceiverSystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CreditReceiverComponent, AfterAutoHandleStateEvent>(OnVendingCashMoneyAfterState);
    }

    private void OnVendingCashMoneyAfterState(EntityUid uid, CreditReceiverComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (_uiSystem.TryGetOpenUi(uid, VendingMachineUiKey.Key, out var bui))
        {
            bui.Update();
        }
    }
}
