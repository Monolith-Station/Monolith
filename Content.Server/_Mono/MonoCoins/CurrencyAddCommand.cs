using System.Linq;
using Content.Server.Administration;
using Content.Server.Database;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server._Mono.MonoCoins;

/// <summary>
/// Admin command for adding MonoCoins to a player.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed partial class CurrencyAddCommand : LocalizedCommands
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private MonoCoinsManager _coins = default!;

    public override string Command => "currency:add";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError("Usage: currency:add <player> <amount>");
            return;
        }

        var playerName = args[0];

        if (!long.TryParse(args[1], out var amount))
        {
            shell.WriteError("Amount must be a valid integer.");
            return;
        }

        // Find the player
        ICommonSession? targetSession = null;
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase))
            {
                targetSession = session;
                break;
            }
        }

        if (targetSession == null)
        {
            shell.WriteError($"Player '{playerName}' not found.");
            return;
        }

        var userId = targetSession.UserId;

        try
        {
            var newBalance = await _coins.AddMonoCoinsAsync(userId, amount);
            shell.WriteLine($"Added {amount} MonoCoins to {playerName}. New balance: {newBalance}");
        }
        catch (Exception ex)
        {
            shell.WriteError($"Database error: {ex.Message}");
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
                var playerNames = _playerManager.Sessions.Select(s => s.Name).ToArray();
                return CompletionResult.FromOptions(playerNames);
            case 2:
                return CompletionResult.FromHint("Amount");
            default:
                return CompletionResult.Empty;
        }
    }
}
