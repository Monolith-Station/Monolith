using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Content.Server.Chat.Systems;
using Robust.Server.Player;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Chat.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class MuteDeadChatCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public string Command => "mutedeadchat";
    public string Description => Loc.GetString("mute-dead-chat-command-description");
    public string Help => Loc.GetString("mute-dead-chat-command-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!_playerManager.TryGetSessionByUsername(args[0], out var player))
        {
            shell.WriteError(Loc.GetString("shell-target-player-does-not-exist"));
            return;
        }

        TimeSpan? expiry = null;
        if (args.Length == 2)
        {
            if (!float.TryParse(args[1], out var minutes) || minutes <= 0)
            {
                shell.WriteError(Loc.GetString("mute-dead-chat-command-invalid-duration"));
                return;
            }

            expiry = _timing.CurTime + TimeSpan.FromMinutes(minutes);
        }

        var chatSystem = _entManager.System<ChatSystem>();
        var muted = chatSystem.MuteDeadChat(player.UserId, expiry);

        if (muted && expiry != null)
        {
            shell.WriteLine(Loc.GetString("mute-dead-chat-command-muted-timed",
                ("player", player.Name),
                ("minutes", args[1])));
        }
        else
        {
            shell.WriteLine(Loc.GetString(
                muted ? "mute-dead-chat-command-muted" : "mute-dead-chat-command-unmuted",
                ("player", player.Name)));
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(players: _playerManager),
                Loc.GetString("mute-dead-chat-command-player-hint"));
        }

        if (args.Length == 2)
            return CompletionResult.FromHint(Loc.GetString("mute-dead-chat-command-duration-hint"));

        return CompletionResult.Empty;
    }
}
