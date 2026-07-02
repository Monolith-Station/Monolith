/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._CE.ZLevels.Core;
using Content.Server.Administration;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels.Mapping.Commands;

public abstract class CEBaseTransitCommand : LocalizedEntityCommands
{
    [Dependency] protected readonly IEntityManager Entities = default!;
    [Dependency] protected readonly CEZLevelsSystem ZLevel = default!;

    protected bool TryGetGrid(IConsoleShell shell, string arg, out Entity<MapGridComponent> grid)
    {
        grid = default;

        if (!NetEntity.TryParse(arg, out var netEnt) ||
            !Entities.TryGetEntity(netEnt, out var uid) ||
            !Entities.TryGetComponent<MapGridComponent>(uid, out var gridComp) ||
            Entities.HasComponent<MapComponent>(uid))
        {
            shell.WriteError($"{arg} is not a grid.");
            return false;
        }

        grid = (uid.Value, gridComp);
        return true;
    }
}

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed class CETransitEnterCommand : CEBaseTransitCommand
{
    public override string Command => "cez-transit-enter";
    public override string Description => "Force a grid into a transit map.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!TryGetGrid(shell, args[0], out var grid))
            return;

        var progress = 1f;
        if (args.Length == 2 && !float.TryParse(args[1], out progress))
        {
            shell.WriteError($"Invalid height {args[1]}.");
            return;
        }

        if (!ZLevel.TryEnterTransit(grid, args.Length == 2 ? progress : null))
        {
            shell.WriteError("Failed to enter transit (are you sure this grid is on a Z-level?)");
            return;
        }

        shell.WriteLine($"Grid {args[0]} is airborne.");
    }
}

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed class CETransitSetCommand : CEBaseTransitCommand
{
    public override string Command => "cez-transit-set";
    public override string Description => "Set a transit map's current distance in the stack.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!TryGetGrid(shell, args[0], out var grid))
            return;

        if (!float.TryParse(args[1], out var altitude))
        {
            shell.WriteError($"Invalid altitude {args[1]}.");
            return;
        }

        if (!ZLevel.SetTransitAltitude(grid, altitude))
        {
            shell.WriteError("Grid is not in transit.");
            return;
        }
    }
}

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed class CETransitDebugCommand : CEBaseTransitCommand
{
    public override string Command => "cez-transit-debug";
    public override string Description => "Does whatever was coded in at the time";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 3)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!TryGetGrid(shell, args[0], out var grid))
            return;

        var amplitude = 1f;
        if (args.Length >= 2 && !float.TryParse(args[1], out amplitude))
        {
            shell.WriteError($"Invalid amplitude {args[1]}.");
            return;
        }

        var period = 10f;
        if (args.Length == 3 && !float.TryParse(args[2], out period))
        {
            shell.WriteError($"Invalid period {args[2]}.");
            return;
        }

        if (!ZLevel.ToggleTransitWave(grid, amplitude, period))
        {
            shell.WriteError("Failed to start wave: grid must be on a z-level map.");
            return;
        }

        shell.WriteLine($"Toggled transit wave on {args[0]} (amplitude {amplitude}, period {period}s).");
    }
}

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed class CETransitLandCommand : CEBaseTransitCommand
{
    public override string Command => "cez-transit-land";
    public override string Description => "Immediately force a grid in transit to land on the nearest Z-layer.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!TryGetGrid(shell, args[0], out var grid))
            return;

        if (!ZLevel.TryExitTransit(grid))
        {
            shell.WriteError("Failed to land (are you sure this grid is on a transitmap?)");
            return;
        }
    }
}
