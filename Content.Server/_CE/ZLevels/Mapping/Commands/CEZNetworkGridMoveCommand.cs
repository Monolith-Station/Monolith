/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._CE.ZLevels.Core;
using Content.Server.Administration;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels.Mapping.Commands;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class CEZNetworkGridMoveCommand : LocalizedEntityCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private CEZLevelsSystem _zLevels = default!;

    public override string Command => "znetwork-grid-move";
    public override string Description => "Move a grid to an absolute depth (position in the sorted zNetwork stack)";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = new List<CompletionOption>();
            var query = _entities.EntityQueryEnumerator<MapGridComponent, TransformComponent, MetaDataComponent>();
            while (query.MoveNext(out var uid, out _, out var xform, out var meta))
            {
                if (xform.MapUid is not { } mapUid || !_entities.HasComponent<CEZMapComponent>(mapUid))
                    continue;

                options.Add(new CompletionOption(_entities.GetNetEntity(uid).ToString(), meta.EntityName));
            }

            return CompletionResult.FromHintOptions(options, "Grid net entity");
        }

        if (args.Length == 2)
            return CompletionResult.FromHint("Target depth (absolute position in the zNetwork stack)");

        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError("Wrong arguments count.");
            return;
        }

        if (!NetEntity.TryParse(args[0], out var targetNet) ||
            !_entities.TryGetEntity(targetNet, out var target))
        {
            shell.WriteError($"Unable to find entity {args[0]}");
            return;
        }

        if (!_entities.HasComponent<MapGridComponent>(target))
        {
            shell.WriteError($"Target entity {args[0]} is not a grid");
            return;
        }

        if (!int.TryParse(args[1], out var targetDepth))
        {
            shell.WriteError($"{args[1]} is not a valid integer depth");
            return;
        }

        var xform = _entities.GetComponent<TransformComponent>(target.Value);
        if (xform.MapUid is not { } mapUid || !_entities.TryGetComponent<CEZMapComponent>(mapUid, out var mapComp))
        {
            shell.WriteError($"Grid {args[0]} is not currently on a map that is part of a zNetwork");
            return;
        }

        var offset = targetDepth - mapComp.Depth;
        if (offset == 0)
        {
            shell.WriteError($"Grid {args[0]} is already at depth {targetDepth}");
            return;
        }

        if (!_zLevels.TryMove(target.Value, offset))
        {
            shell.WriteError($"Failed to move grid {args[0]} to depth {targetDepth}. Is there a map at that depth in the zNetwork?");
            return;
        }

        shell.WriteLine($"Moved grid {args[0]} to depth {targetDepth}.");
    }
}
