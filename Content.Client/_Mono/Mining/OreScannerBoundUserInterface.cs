using Content.Shared._Mono.Mining;
using Content.Client.Administration.Managers;
using Content.Shared.Administration;
using Robust.Client.ViewVariables;
using Robust.Shared.ViewVariables;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._Mono.Mining;

[UsedImplicitly]
public sealed class OreScannerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private OreScannerWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<OreScannerWindow>();
        _window.Map.SetConsole(Owner);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is OreScannerState scan)
            _window?.Map.UpdateScan(scan);
    }
}
