using Content.Client._Mono.PDV.UI;
using Content.Shared._Mono.PDV.Components;
using JetBrains.Annotations;

namespace Content.Client._Mono.PDV.BUI;

[UsedImplicitly]
public sealed class HereticConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private HereticMenu? _menu;

    public HereticConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = new();

        _menu.OnClose += Close;

        _menu.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState message)
    {
        base.UpdateState(message);

        if (message is not HereticConsoleState state)
            return;

        _menu?.UpdateEntries(state.Heretics);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _menu?.Dispose();
    }
}
