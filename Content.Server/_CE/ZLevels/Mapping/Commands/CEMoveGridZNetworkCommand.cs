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
public sealed class CEMoveGridZNetworkCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly CEZLevelsSystem _zLevel = default!;

    public override string Command => "znetwork-move-grid";
    public override string Description => "Move a grid up or down its z-network by the given offset.";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
                var options = new List<CompletionOption>();
                var query = _entities.EntityQueryEnumerator<MapGridComponent, MetaDataComponent>();
                while (query.MoveNext(out var uid, out _, out var meta))
                {
                    if (_entities.HasComponent<MapComponent>(uid))
                        continue;

                    options.Add(new CompletionOption(_entities.GetNetEntity(uid).ToString(), meta.EntityName));
                }
                return CompletionResult.FromHintOptions(options, "grid net entity");
            case 2:
                return CompletionResult.FromHint("offset (e.g. 1 = up, -1 = down)");
        }
        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var targetNet) ||
            !_entities.TryGetEntity(targetNet, out var target))
        {
            shell.WriteError($"Unable to find entity {args[0]}");
            return;
        }

        if (!_entities.HasComponent<MapGridComponent>(target) ||
            _entities.HasComponent<MapComponent>(target))
        {
            shell.WriteError($"Entity {args[0]} is not a grid.");
            return;
        }

        if (!int.TryParse(args[1], out var offset) || offset == 0)
        {
            shell.WriteError($"Invalid offset {args[1]}.");
            return;
        }

        if (!_entities.TryGetComponent<TransformComponent>(target, out var xform) ||
            !_entities.TryGetComponent<CEZLevelMapComponent>(xform.MapUid, out var zMap))
        {
            shell.WriteError("Grid is not on a z-level map.");
            return;
        }

        if (!_zLevel.TryMove(target.Value, offset, (xform.MapUid.Value, zMap)))
        {
            shell.WriteError($"Failed to move grid: no z-level at depth {zMap.Depth + offset}?");
            return;
        }

        shell.WriteLine($"Moved grid {args[0]} to depth {zMap.Depth + offset}.");
    }
}
