using Content.Client._Mono.Shipyard.UI;
using Robust.Shared.Console;

namespace Content.Client._Mono.Shipyard;

public sealed class VesselReferenceSystem : EntitySystem
{
    [Dependency] private IConsoleHost _console = default!;

    private VesselReferenceWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        _console.RegisterCommand("vesselref", "Opens the unit reference window.", "vesselref", VesselRefCommand);
    }

    public override void Shutdown()
    {
        _console.UnregisterCommand("vesselref");
        base.Shutdown();
    }

    private void VesselRefCommand(IConsoleShell shell, string argStr, string[] args)
    {
        ToggleWindow();
    }

    public void ToggleWindow()
    {
        if (_window != null && !_window.Disposed && _window.IsOpen)
        {
            _window.Close();
            return;
        }

        if (_window == null || _window.Disposed)
            _window = new VesselReferenceWindow();

        _window.Rebuild();
        _window.OpenCentered();
    }
}
