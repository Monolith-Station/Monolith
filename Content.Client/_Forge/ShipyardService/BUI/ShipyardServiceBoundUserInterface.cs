using Content.Client._Forge.ShipyardService.UI;
using Content.Shared._Forge.ShipyardService;
using Robust.Client.UserInterface;

namespace Content.Client._Forge.ShipyardService.BUI;

public sealed class ShipyardServiceBoundUserInterface : BoundUserInterface
{
    private ShipyardServiceWindow? _window;
    private ShipyardServiceGridWindow? _gridWindow;
    private ShipyardServiceBoundUserInterfaceState _state = new();

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
        _window.OpenGridPressed += OpenGridWindow;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not ShipyardServiceBoundUserInterfaceState serviceState)
            return;

        _state = serviceState;
        _window?.UpdateState(serviceState);
        _gridWindow?.UpdateState(serviceState);
    }

    private void OpenGridWindow()
    {
        if (_gridWindow != null && !_gridWindow.Disposed)
        {
            _gridWindow.MoveToFront();
            return;
        }

        _gridWindow = new ShipyardServiceGridWindow();
        _gridWindow.ApplyMarked += targets => SendMessage(new ShipyardServiceUpgradeMarkedMessage(targets));
        _gridWindow.OnClose += () => _gridWindow = null;
        _gridWindow.UpdateState(_state);
        _gridWindow.OpenCenteredRight();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _gridWindow?.Dispose();
        _gridWindow = null;
    }
}
