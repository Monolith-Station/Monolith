using static Content.Shared._Mono.Shipyard.SharedPreview;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client._Mono.Shipyard.UI;

public sealed class ShipyardPreviewBoundUserInterface : BoundUserInterface
{
    private ShipyardPreviewMenu? _menu;
    [Dependency] private ShipyardPreviewSystem _preview = default!;

    public ShipyardPreviewBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {

    }

    protected override void Open()
    {
        base.Open();

        _menu = new ShipyardPreviewMenu();
        _menu.OpenCentered();
        _menu.UpdateMenu();

        _menu.OnExit += Exit;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        _menu?.Dispose();
    }

    private void Exit(ButtonEventArgs args)
    {
        SendMessage(new ShipyardPreviewExitMessage());
        _preview.Dispose();
    }
}
