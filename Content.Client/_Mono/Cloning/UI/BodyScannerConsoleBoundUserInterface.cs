using Content.Shared._Mono.Cloning;
using Robust.Client.UserInterface;

namespace Content.Client._Mono.Cloning.UI;

public sealed class BodyScannerConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private BodyScannerConsoleWindow? _window;

    public BodyScannerConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BodyScannerConsoleWindow>();
        _window.ScanButton.OnPressed += _ => SendMessage(new BodyScannerScanMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is BodyScannerConsoleBoundUserInterfaceState cast)
            _window?.Populate(cast);
    }
}
