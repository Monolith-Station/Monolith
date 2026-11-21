using Content.Shared._Mono.Ships;
using Robust.Client.UserInterface;

namespace Content.Client._Mono.ShipShieldShell;

public sealed class ShipShieldProjectorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ShipShieldProjectorWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ShipShieldProjectorWindow>();
        _window.SetEntity(Owner);
        _window.ToggleRequested += () => SendPredictedMessage(new ShipShieldProjectorToggleMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ShipShieldProjectorUiState projector)
            _window?.UpdateState(projector);
    }
}
