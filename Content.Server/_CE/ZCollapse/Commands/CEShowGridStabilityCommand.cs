/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._CE.ZCollapse.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class CEShowGridStabilityCommand : LocalizedEntityCommands
{
    [Dependency] private CEZCollapseSystem _collapse = default!;

    public override string Command => "showgridstability";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var session = shell.Player;
        if (session == null)
            return;

        _collapse.ToggleDebugView(session);
    }
}
