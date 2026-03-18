using System.Linq;
using Content.Server._Mono.Grid;
using Content.Server.Administration;
using Content.Shared._Mono.Grid;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Administration.Commands;

/// <summary>
/// Command that allows you to apply grid modifiers to existing grids
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class AddGridModifier : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "addgridmodifier";
    public string Description => "Applies grid modification to chosen grid.";
    public string Help => $"Usage: {Command} <gridUid> <modification...>";
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {

        if (args.Length < 2)
            shell.WriteLine($"Not enough arguments.\n{Help}");

        if (!NetEntity.TryParse(args[0], out var uidNet) || !_entManager.TryGetEntity(uidNet, out var uid))
        {
            shell.WriteLine($"Invalid entity id.");
            return;
        }

        if (!_entManager.TryGetComponent(uid, out MapGridComponent? map))
        {
            shell.WriteLine($"Entity is not a grid.");
            return;
        }

        List<ProtoId<GridModificationPrototype>> modifiers = [];
        var gridMod = _entManager.System<GridModifierSystem>();

        foreach (var mod in args.Skip(1))
        {
            modifiers.Add(mod);
        }

        gridMod.ModifyGrid(uid.Value, modifiers);
    }
}
