using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server._Mono.MonoCoins;

/// <summary>
/// Player command for transferring MonoCoins to other players.
/// </summary>
[AnyCommand]
public sealed partial class CurrencyTransferCommand : LocalizedCommands
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private MonoCoinsManager _coins = default!;
    [Dependency] private IChatManager _chatManager = default!;

    public override string Command => "currency:transfer";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError("Usage: currency:transfer <player> <amount>");
            return;
        }

        var targetPlayerName = args[0];

        if (!long.TryParse(args[1], out var amount))
        {
            shell.WriteError("Amount must be a valid integer.");
            return;
        }

        if (amount <= 0)
        {
            shell.WriteError("Amount must be positive.");
            return;
        }

        // Get the sender (the player executing the command)
        var senderSession = shell.Player as ICommonSession;
        if (senderSession == null)
        {
            shell.WriteError("This command can only be used by players.");
            return;
        }

        // Find the target player
        ICommonSession? targetSession = null;
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Name.Equals(targetPlayerName, StringComparison.OrdinalIgnoreCase))
            {
                targetSession = session;
                break;
            }
        }

        if (targetSession == null)
        {
            shell.WriteError($"Player '{targetPlayerName}' not found or not online.");
            return;
        }

        // Prevent self-transfer
        if (senderSession.UserId == targetSession.UserId)
        {
            shell.WriteError("You cannot transfer currency to yourself.");
            return;
        }

        var senderUserId = senderSession.UserId;
        var targetUserId = targetSession.UserId;

        try
        {
            // Check if sender has enough MonoCoins
            var senderBalance = await _coins.GetMonoCoinsBalanceAsync(senderUserId);
            if (senderBalance < amount)
            {
                shell.WriteError($"Insufficient currency. You have ${senderBalance}, cannot transfer ${amount}.");
                return;
            }

            // Perform the transfer (subtract from sender, add to target)
            var newSenderBalance = await _coins.AddMonoCoinsAsync(senderUserId, -amount);
            var newTargetBalance = await _coins.AddMonoCoinsAsync(targetUserId, amount);

            // Notify both players
            shell.WriteLine($"Successfully transferred ${amount} to {targetPlayerName}. New balance: ${newSenderBalance}");

            // Notify the target player via chat
            var notificationMessage = $"Received ${amount} from {senderSession.Name}. New balance: ${newTargetBalance}";
            _chatManager.ChatMessageToOne(
                ChatChannel.Notifications,
                notificationMessage,
                notificationMessage,
                EntityUid.Invalid,
                false,
                targetSession.Channel);
        }
        catch (Exception ex)
        {
            shell.WriteError($"Transfer failed due to database error: {ex.Message}");
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
