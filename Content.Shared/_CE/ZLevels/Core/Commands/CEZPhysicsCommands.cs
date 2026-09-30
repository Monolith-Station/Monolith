/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Globalization;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using Robust.Shared.Console;

namespace Content.Shared._CE.ZLevels.Core.Commands;

/// <summary>
/// Debug command to set the Z-velocity of an entity. Lives in Shared so the client
/// applies it immediately (predicted) and mirrors it to the server for the
/// authoritative run. Wakes the body, so it works without touching SleepThreshold.
/// </summary>
[AnyCommand]
public sealed partial class CEZSetVelocityCommand : LocalizedEntityCommands
{
    [Dependency] private ISharedAdminManager _admin = default!;
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;

    public override string Command => "cez-zvel";
    public override string Description => "Sets the Z-velocity of an entity. Positive = up. Wakes the body.";
    public override string Help => "cez-zvel <velocity> | cez-zvel <entity> <velocity>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is { } player && !_admin.HasAdminFlag(player, AdminFlags.Debug))
        {
            shell.WriteError("You need the +DEBUG admin flag to use this command.");
            return;
        }

        EntityUid? target;
        string velArg;

        switch (args.Length)
        {
            case 1:
                target = shell.Player?.AttachedEntity;
                velArg = args[0];

                if (target == null)
                {
                    shell.WriteError("No attached entity — specify one: cez-zvel <entity> <velocity>");
                    return;
                }

                break;
            case 2:
                if (!NetEntity.TryParse(args[0], out var netEnt) ||
                    !EntityManager.TryGetEntity(netEnt, out target))
                {
                    shell.WriteError($"{args[0]} is not a valid entity.");
                    return;
                }

                velArg = args[1];
                break;
            default:
                shell.WriteError(Help);
                return;
        }

        if (!float.TryParse(velArg, NumberStyles.Float, CultureInfo.InvariantCulture, out var velocity))
        {
            shell.WriteError($"{velArg} is not a valid float.");
            return;
        }

        if (!EntityManager.TryGetComponent<CEZPhysicsComponent>(target.Value, out var zPhys))
        {
            shell.WriteError($"{EntityManager.ToPrettyString(target.Value)} has no {nameof(CEZPhysicsComponent)}.");
            return;
        }

        _zLevels.SetZVelocity((target.Value, zPhys), velocity);
        shell.WriteLine($"Z-velocity of {EntityManager.ToPrettyString(target.Value)} set to {velocity.ToString(CultureInfo.InvariantCulture)}.");

        // Local client execution is just a prediction — the dirtied field would be
        // stomped by the next server state. Mirror it to the server for the real run.
        if (shell.IsClient)
            shell.RemoteExecuteCommand($"{Command} {string.Join(' ', args)}");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint("<velocity> | <entity>"),
            2 => CompletionResult.FromHint("<velocity>"),
            _ => CompletionResult.Empty,
        };
    }
}
