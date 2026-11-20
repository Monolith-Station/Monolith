using Content.Client._Mono.Shipyard.UI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Client._Mono.Shipyard;
[AnyCommand]
public sealed partial class UnitRefCommand : LocalizedCommands
{
    private VesselReferenceWindow? _window;
    public override string Command => "unitref";

    public override string Description => Loc.GetString("unitref-command-description");

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
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
