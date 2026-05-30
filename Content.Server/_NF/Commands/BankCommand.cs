using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Database;
using Content.Server.Preferences.Managers;
using Content.Server._NF.Bank;
using Content.Shared.Administration;
using Content.Shared.Preferences;
using Content.Shared._NF.Bank.Components;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._NF.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class BankCommand : IConsoleCommand
{
    [Dependency] private IServerPreferencesManager _prefsManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IServerDbManager _dbManager = default!;
    [Dependency] private IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    public string Command => "bank";

    public string Description => "Modifies a player's bank account balance.";

    public string Help => "bank <username/character> <amount>\n" +
                          "Adds or removes the specified amount from the player's bank account. " +
                          "Use positive values to add money, negative values to remove money. " +
                          "Works with online players, offline players in cache, and players in database.";

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError("Wrong number of arguments.\nUsage: " + Help);
            return;
        }

        var target = args[0];
        var bankSystem = _entitySystemManager.GetEntitySystem<BankSystem>();

        if (!int.TryParse(args[1], out var amount))
        {
            shell.WriteError("Amount must be a valid number.");
            return;
        }

        // First try online players by name
        var onlinePlayer = _playerManager.Sessions
            .FirstOrDefault(s => s.Name.Equals(target, StringComparison.OrdinalIgnoreCase));

        if (onlinePlayer != null)
        {
            // Handle online player
            await HandleOnlinePlayer(shell, onlinePlayer, amount, target);
            return;
        }

        // If player name not found online, try by ID
        if (Guid.TryParse(target, out var userId))
        {
            onlinePlayer = _playerManager.Sessions.FirstOrDefault(s => s.UserId == userId);
            if (onlinePlayer != null)
            {
                await HandleOnlinePlayer(shell, onlinePlayer, amount, target);
                return;
            }
        }

        // If not online, check cached preferences for offline players
        if (TryGetOfflinePlayerData(target, out var offlineUserId, out var offlinePrefs, out var offlineProfile))
        {
            await HandleOfflinePlayer(shell, offlineUserId, offlinePrefs, offlineProfile, amount, target);
            return;
        }

        // If not in cache, try the database
        var record = await _dbManager.GetPlayerRecordByUserName(target);
        if (record != null)
        {
            var dbUserId = record.UserId;
            var dbPrefs = await _dbManager.GetPlayerPreferencesAsync(dbUserId, default);
            if (dbPrefs != null &&
                dbPrefs.SelectedCharacterIndex >= 0 &&
                dbPrefs.Characters.TryGetValue(dbPrefs.SelectedCharacterIndex, out var dbProfile))
            {
                if (dbProfile is HumanoidCharacterProfile dbHumanoid)
                {
                    await HandleOfflinePlayer(shell, dbUserId, dbPrefs, dbHumanoid, amount, target);
                    return;
                }
            }
        }

        shell.WriteError($"Unable to find player or character '{target}'.");
    }

    private async Task HandleOnlinePlayer(IConsoleShell shell, ICommonSession targetSession, int amount, string target)
    {
        var bankSystem = _entitySystemManager.GetEntitySystem<BankSystem>();

        if (!_prefsManager.TryGetCachedPreferences(targetSession.UserId, out var prefs))
        {
            shell.WriteError($"Unable to retrieve preferences for player '{target}'.");
            return;
        }

        if (prefs.SelectedCharacter is not HumanoidCharacterProfile profile)
        {
            shell.WriteError($"Player '{target}' has an invalid character profile.");
            return;
        }

        var currentBalance = profile.BankBalance;

        // Ensure the player won't have negative balance after withdrawal
        if (amount < 0 && Math.Abs(amount) > currentBalance)
        {
            shell.WriteError($"Player '{target}' only has {currentBalance}, cannot remove {Math.Abs(amount)}.");
            return;
        }

        bool success;
        int? newBalance = null;

        // Check if player is currently in-game with an entity
        EntityUid? playerEntity = targetSession.AttachedEntity;

        if (playerEntity != null && _entityManager.HasComponent<BankAccountComponent>(playerEntity.Value))
        {
            // Player is in-game with entity that has bank account - use entity methods which will update the profile
            if (amount > 0)
            {
                success = bankSystem.TryBankDeposit(playerEntity.Value, amount, false);
                if (success)
                {
                    // Get updated balance after deposit
                    success = bankSystem.TryGetBalance(targetSession, out int updatedBalance);
                    if (success)
                        newBalance = updatedBalance;
                }
            }
            else if (amount < 0)
            {
                success = bankSystem.TryBankWithdraw(playerEntity.Value, Math.Abs(amount));
                if (success)
                {
                    // Get updated balance after withdrawal
                    success = bankSystem.TryGetBalance(targetSession, out int updatedBalance);
                    if (success)
                        newBalance = updatedBalance;
                }
            }
            else
            {
                shell.WriteLine($"Player '{target}' balance unchanged: {currentBalance}");
                return;
            }

            if (success)
                shell.WriteLine("Updated player's bank account.");
        }
        else
        {
            // Player is not in-game or entity has no bank account - update profile directly
            if (amount > 0)
            {
                success = bankSystem.TryBankDeposit(targetSession, prefs, profile, amount, out newBalance);
            }
            else if (amount < 0)
            {
                success = bankSystem.TryBankWithdraw(targetSession, prefs, profile, Math.Abs(amount), out newBalance);
            }
            else
            {
                shell.WriteLine($"Player '{target}' balance unchanged: {currentBalance}");
                return;
            }
        }

        if (!success || newBalance == null)
        {
            shell.WriteError($"Failed to modify player '{target}' bank balance.");
            return;
        }

        shell.WriteLine(amount > 0
            ? $"Added {amount} to player '{target}' balance. New balance: {newBalance.Value}"
            : $"Removed {Math.Abs(amount)} from player '{target}' balance. New balance: {newBalance.Value}");
    }

    private async Task HandleOfflinePlayer(IConsoleShell shell, NetUserId userId, PlayerPreferences prefs, HumanoidCharacterProfile profile, int amount, string target)
    {
        var bankSystem = _entitySystemManager.GetEntitySystem<BankSystem>();
        var currentBalance = profile.BankBalance;

        // Ensure the player won't have negative balance after withdrawal
        if (amount < 0 && Math.Abs(amount) > currentBalance)
        {
            shell.WriteError($"Player '{target}' only has {currentBalance}, cannot remove {Math.Abs(amount)}.");
            return;
        }

        if (amount == 0)
        {
            shell.WriteLine($"Player '{target}' balance unchanged: {currentBalance}");
            return;
        }

        bool success;
        int? newBalance = null;

        // Use the new offline bank methods
        if (amount > 0)
        {
            success = await bankSystem.TryBankDepositOffline(userId, prefs, profile, amount);
            if (success)
                newBalance = currentBalance + amount;
        }
        else
        {
            success = await bankSystem.TryBankWithdrawOffline(userId, prefs, profile, Math.Abs(amount));
            if (success)
                newBalance = currentBalance - Math.Abs(amount);
        }

        if (!success || newBalance == null)
        {
            shell.WriteError($"Failed to modify offline player '{target}' bank balance.");
            return;
        }

        shell.WriteLine(amount > 0
            ? $"Added {amount} to offline player '{target}' balance. New balance: {newBalance.Value}"
            : $"Removed {Math.Abs(amount)} from offline player '{target}' balance. New balance: {newBalance.Value}");
    }

    private bool TryGetOfflinePlayerData(string username, out NetUserId userId, out PlayerPreferences prefs, out HumanoidCharacterProfile profile)
    {
        userId = default;
        prefs = null!;
        profile = null!;

        // Check all users in the preferences cache
        foreach (var playerData in _playerManager.GetAllPlayerData())
        {
            if (_prefsManager.TryGetCachedPreferences(playerData.UserId, out var cachedPrefs))
            {
                foreach (var (_, characterProfile) in cachedPrefs.Characters)
                {
                    if (characterProfile is HumanoidCharacterProfile humanoid &&
                        humanoid.Name.Equals(username, StringComparison.OrdinalIgnoreCase))
                    {
                        userId = playerData.UserId;
                        prefs = cachedPrefs;
                        profile = humanoid;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = new List<string>();

            // Add online players
            options.AddRange(_playerManager.Sessions.Select(s => s.Name));

            // Add players from cached preferences
            foreach (var playerData in _playerManager.GetAllPlayerData())
            {
                if (_prefsManager.TryGetCachedPreferences(playerData.UserId, out var prefs))
                {
                    foreach (var (_, profile) in prefs.Characters)
                    {
                        if (profile is HumanoidCharacterProfile humanoid)
                        {
                            options.Add(humanoid.Name);
                        }
                    }
                }
            }

            return CompletionResult.FromHintOptions(options.Distinct(), "<username/character>");
        }

        if (args.Length == 2)
        {
            // For the amount parameter, provide some common values as suggestions
            var amountOptions = new List<CompletionOption>
            {
                new CompletionOption("100", "Add 100 credits"),
                new CompletionOption("1000", "Add 1000 credits"),
                new CompletionOption("10000", "Add 10000 credits"),
                new CompletionOption("-100", "Remove 100 credits"),
                new CompletionOption("-1000", "Remove 1000 credits")
            };

            return CompletionResult.FromOptions(amountOptions);
        }

        return CompletionResult.Empty;
    }
}
