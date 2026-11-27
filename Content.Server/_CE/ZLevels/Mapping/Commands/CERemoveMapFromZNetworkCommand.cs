/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._CE.ZLevels.Core;
using Content.Server.Administration;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._CE.ZLevels.Mapping.Commands;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class CERemoveMapFromZNetworkCommand : LocalizedEntityCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private CEZLevelsSystem _levels = default!;

    public override string Command => "znetwork-remove";
    public override string Description => "Detach a map from a Z-level network without deleting it.";
    public override string Help => $"{Command} <network entity> <depth>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var net) || !_entities.TryGetEntity(net, out var uid) ||
            !_entities.TryGetComponent<CEZMapNetworkComponent>(uid, out var network) ||
            !int.TryParse(args[1], out var depth))
        {
            shell.WriteError(Help);
            return;
        }

        var target = (uid.Value, network);
        if (!_levels.TryGetMapAtDepth(target, depth, out var map) || !_levels.TryRemoveMapFromNetwork(target, map))
        {
            shell.WriteError($"Failed to remove map at depth {depth} from z-network.");
            return;
        }

        shell.WriteLine($"Detached map at depth {depth} without deleting it.");
    }
}
