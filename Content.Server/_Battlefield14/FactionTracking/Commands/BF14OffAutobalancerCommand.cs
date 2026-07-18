using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Battlefield14.FactionTracking.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class BF14OffAutobalancerCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "BF14offautobalancer";
    public string Description => "Toggles the faction autobalancer on/off. When disabled, players can join any team freely.";
    public string Help => $"{Command}";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var factionTracking = _entManager.System<FactionTrackingSystem>();
        factionTracking.AutobalancerEnabled = !factionTracking.AutobalancerEnabled;

        var state = factionTracking.AutobalancerEnabled ? "enabled" : "disabled";
        shell.WriteLine($"Autobalancer is now {state}.");
    }
}
