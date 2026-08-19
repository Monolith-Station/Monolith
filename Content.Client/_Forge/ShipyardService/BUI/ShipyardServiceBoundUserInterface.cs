using Content.Client._Forge.ShipyardService.UI;
using Content.Shared._Forge.ShipyardService;
using Robust.Client.UserInterface;

namespace Content.Client._Forge.ShipyardService.BUI;

public sealed class ShipyardServiceBoundUserInterface : BoundUserInterface
{
    private ShipyardServiceWindow? _window;

    public ShipyardServiceBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindowCenteredLeft<ShipyardServiceWindow>();
        _window.RepairPressed += () => SendMessage(new ShipyardServicePurchaseMessage(ShipyardServiceAction.Repair));
        _window.UpgradePartsPressed += () => SendMessage(new ShipyardServicePurchaseMessage(ShipyardServiceAction.UpgradeParts));
        _window.ReinforcePressed += () => SendMessage(new ShipyardServicePurchaseMessage(ShipyardServiceAction.Reinforce));
        _window.PlastitaniumPressed += () => SendMessage(new ShipyardServicePurchaseMessage(ShipyardServiceAction.Plastitanium));
        _window.ShuttleSelected += shuttle => SendMessage(new ShipyardServiceSelectMessage(shuttle));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not ShipyardServiceBoundUserInterfaceState serviceState)
            return;

        _window?.UpdateState(serviceState);
    }
}
