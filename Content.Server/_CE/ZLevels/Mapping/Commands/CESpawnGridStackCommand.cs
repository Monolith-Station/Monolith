/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Mapping.Prototypes;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.ZLevels.Mapping.Commands;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class CESpawnGridStackCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly CEZGridStackSystem _gridStack = default!;

    public override string Command => "cez-spawn-stack";

    public override string Description =>
        "Spawn a multi-deck grid stack into a z-network. Args: <zNetwork net entity> <baseDepth> <cezGridStack id> [x] [y]";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
                var nets = new List<CompletionOption>();
                var query = _entities.EntityQueryEnumerator<CEZMapNetworkComponent, MetaDataComponent>();
                while (query.MoveNext(out var uid, out _, out var meta))
                    nets.Add(new CompletionOption(_entities.GetNetEntity(uid).ToString(), meta.EntityName));
                return CompletionResult.FromHintOptions(nets, "zNetwork net entity");
            case 2:
                return CompletionResult.FromHint("<baseDepth (int)>");
            case 3:
                return CompletionResult.FromHintOptions(
                    CompletionHelper.PrototypeIDs<CEZGridStackPrototype>(proto: _proto), "<cezGridStack id>");
            case 4:
                return CompletionResult.FromHint("[x]");
            case 5:
                return CompletionResult.FromHint("[y]");
            default:
                return CompletionResult.Empty;
        }
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 3 or > 5)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netEnt) ||
            !_entities.TryGetEntity(netEnt, out var networkUid) ||
            !_entities.TryGetComponent<CEZMapNetworkComponent>(networkUid, out var network))
        {
            shell.WriteError($"'{args[0]}' is not a z-network entity.");
            return;
        }

        if (!int.TryParse(args[1], out var baseDepth))
        {
            shell.WriteError($"'{args[1]}' is not an integer depth.");
            return;
        }

        var pos = Vector2.Zero;
        if (args.Length >= 4 && !float.TryParse(args[3], out pos.X))
        {
            shell.WriteError($"'{args[3]}' is not a number.");
            return;
        }

        if (args.Length == 5 && !float.TryParse(args[4], out pos.Y))
        {
            shell.WriteError($"'{args[4]}' is not a number.");
            return;
        }

        var spawned = new List<EntityUid>();
        if (!_gridStack.TrySpawnGridStack(args[2], (networkUid.Value, network), baseDepth, pos, spawned))
        {
            shell.WriteError($"Failed to spawn grid stack '{args[2]}' (see server log).");
            return;
        }

        shell.WriteLine($"Spawned {spawned.Count} decks of '{args[2]}' from depth {baseDepth} at {pos}.");
    }
}
