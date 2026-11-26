// Content.Server/_Mono/Sensors/Commands/SensorsDebugCommand.cs
using Content.Server.Administration;
using Content.Shared._Mono.Sensors;
using Content.Shared.Administration;
using JetBrains.Annotations;
using Robust.Shared.Console;

namespace Content.Server._Mono.Sensors.Commands;

[UsedImplicitly]
[AdminCommand(AdminFlags.Debug)]
public sealed partial class SensorsDebugCommand : LocalizedEntityCommands
{
    [Dependency] private IEntityManager _ent = default!;

    public override string Command => "sensorsdebug";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var player = shell.Player;
        if (player?.AttachedEntity is not { } mob)
        {
            shell.WriteError("No attached entity.");
            return;
        }

        var xform = _ent.GetComponent<TransformComponent>(mob);
        if (xform.GridUid is not { } grid)
        {
            shell.WriteError("Not on a grid.");
            return;
        }

        if (!_ent.TryGetComponent<GridSensorsComponent>(grid, out var sensors))
        {
            shell.WriteError("Grid has no GridSensorsComponent.");
            return;
        }

        shell.WriteLine($"Grid {grid} contacts={sensors.Contacts.Count} codes=[{string.Join(",", sensors.DatalinkCodes)}]");
        foreach (var (_, c) in sensors.Contacts)
        {
            shell.WriteLine(
                $"  {c.Designation} net={c.TargetGrid} range={c.Range:F0} level={c.Level} flags={c.Flags}");
        }
    }
}
