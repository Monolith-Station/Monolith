using System.Linq;
using System.Text;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Shared.Console;

namespace Content.Server._Mono.Administration.DeadChatShadowban;

[AdminCommand(AdminFlags.Ban)]
public sealed partial class DeadChatShadowbanCommand : LocalizedCommands
{
    [Dependency] private IPlayerLocator _playerLocator = default!;
    [Dependency] private DeadChatShadowbanManager _shadowbans = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    public override string Command => "deadchatshadowban";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteError(Loc.GetString("shell-need-minimum-one-argument"));
            shell.WriteLine(Help);
            return;
        }

        var data = await _playerLocator.LookupIdByNameOrIdAsync(args[0]);
        if (data == null)
        {
            shell.WriteError(Loc.GetString("cmd-deadchatshadowban-not-found", ("username", args[0])));
            return;
        }

        var reason = string.Join(' ', args.Skip(1)).Trim();
        var adminName = shell.Player?.Name ?? "Server";

        if (!_shadowbans.AddShadowban(data.UserId, data.Username, adminName, reason))
        {
            shell.WriteLine(Loc.GetString("cmd-deadchatshadowban-existing", ("username", data.Username)));
            return;
        }

        _adminLog.Add(LogType.AdminCommands,
            LogImpact.High,
            $"{adminName} ({shell.Player?.UserId.ToString() ?? "console"}) deadchat shadowbanned {data.Username} ({data.UserId}). Reason: {reason}");

        shell.WriteLine(Loc.GetString("cmd-deadchatshadowban-added", ("username", data.Username)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), Loc.GetString("cmd-deadchatshadowban-arg-player")),
            > 1 => CompletionResult.FromHint(Loc.GetString("cmd-deadchatshadowban-arg-reason")),
            _ => CompletionResult.Empty,
        };
    }
}

[AdminCommand(AdminFlags.Ban)]
public sealed partial class DeadChatUnshadowbanCommand : LocalizedCommands
{
    [Dependency] private IPlayerLocator _playerLocator = default!;
    [Dependency] private DeadChatShadowbanManager _shadowbans = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    public override string Command => "deadchatunshadowban";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-need-exactly-one-argument"));
            shell.WriteLine(Help);
            return;
        }

        var data = await _playerLocator.LookupIdByNameOrIdAsync(args[0]);
        if (data == null)
        {
            shell.WriteError(Loc.GetString("cmd-deadchatunshadowban-not-found", ("username", args[0])));
            return;
        }

        if (!_shadowbans.RemoveShadowban(data.UserId))
        {
            shell.WriteLine(Loc.GetString("cmd-deadchatunshadowban-existing", ("username", data.Username)));
            return;
        }

        var adminName = shell.Player?.Name ?? "Server";
        _adminLog.Add(LogType.AdminCommands,
            LogImpact.High,
            $"{adminName} ({shell.Player?.UserId.ToString() ?? "console"}) removed the deadchat shadowban on {data.Username} ({data.UserId})");

        shell.WriteLine(Loc.GetString("cmd-deadchatunshadowban-removed", ("username", data.Username)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), Loc.GetString("cmd-deadchatshadowban-arg-player"));

        return CompletionResult.Empty;
    }
}

[AdminCommand(AdminFlags.Ban)]
public sealed partial class DeadChatShadowbanListCommand : LocalizedCommands
{
    [Dependency] private DeadChatShadowbanManager _shadowbans = default!;

    public override string Command => "deadchatshadowbans";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var shadowbans = _shadowbans.GetShadowbans().ToList();
        if (shadowbans.Count == 0)
        {
            shell.WriteLine(Loc.GetString("cmd-deadchatshadowbans-none"));
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine(Loc.GetString("cmd-deadchatshadowbans-header", ("count", shadowbans.Count)));

        foreach (var ban in shadowbans)
        {
            sb.AppendLine(Loc.GetString("cmd-deadchatshadowbans-entry",
                ("username", ban.Username),
                ("userId", ban.UserId.ToString()),
                ("admin", ban.AdminName),
                ("date", ban.CreatedAt.ToString("yyyy-MM-dd HH:mm")),
                ("reason", string.IsNullOrWhiteSpace(ban.Reason) ? Loc.GetString("cmd-deadchatshadowbans-no-reason") : ban.Reason)));
        }

        shell.WriteLine(sb.ToString().TrimEnd());
    }
}
