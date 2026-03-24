using System.Linq;
using Content.Server.Administration;
using Content.Server.Database;
using Content.Shared._Mono.Company;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Company;

[AdminCommand(AdminFlags.Whitelist)]
public sealed class CompanyWhitelistAddCommand : LocalizedCommands
{
    [Dependency] private readonly CompanyManager _company = default!;
    [Dependency] private readonly IPlayerLocator _playerLocator = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override string Command => "companywhitelistadd";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number-need-specific",
                ("properAmount", 2),
                ("currentAmount", args.Length)));
            shell.WriteLine(Help);
            return;
        }

        var player = args[0].Trim();
        var company = new ProtoId<CompanyPrototype>(args[1].Trim());
        if (!_prototypes.TryIndex(company, out var companyPrototype))
        {
            shell.WriteError(Loc.GetString("cmd-companywhitelist-company-does-not-exist", ("company", company.Id)));
            shell.WriteLine(Help);
            return;
        }

        var data = await _playerLocator.LookupIdByNameAsync(player);
        if (data != null)
        {
            var guid = data.UserId;
            var isWhitelisted = _company.IsWhitelisted(guid, company);
            if (isWhitelisted)
            {
                shell.WriteLine(Loc.GetString("cmd-companywhitelistadd-already-whitelisted",
                    ("player", player),
                    ("companyId", company.Id),
                    ("companyName", companyPrototype.Name)));
                return;
            }

            _company.AddWhitelist(guid, company);
            shell.WriteLine(Loc.GetString("cmd-companywhitelistadd-added",
                ("player", player),
                ("companyId", company.Id),
                ("companyName", companyPrototype.Name)));
            return;
        }

        shell.WriteError(Loc.GetString("cmd-companywhitelist-player-not-found", ("player", player)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                _players.Sessions.Select(s => s.Name),
                Loc.GetString("cmd-companywhitelist-hint-player"));
        }

        if (args.Length == 2)
        {
            return CompletionResult.FromHintOptions(
                _prototypes.EnumeratePrototypes<CompanyPrototype>()
                    .Where(p => p.Whitelisted)
                    .Select(p => p.ID),
                Loc.GetString("cmd-companywhitelist-hint-company"));
        }

        return CompletionResult.Empty;
    }
}

[AdminCommand(AdminFlags.Whitelist)]
public sealed class GetCompanyWhitelistCommand : LocalizedCommands
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPlayerLocator _playerLocator = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public override string Command => "companywhitelistget";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteError("This command needs at least one argument.");
            shell.WriteLine(Help);
            return;
        }

        var player = string.Join(' ', args).Trim();
        var data = await _playerLocator.LookupIdByNameAsync(player);
        if (data != null)
        {
            var guid = data.UserId;
            var whitelists = await _db.GetCompanyWhitelists(guid);
            if (whitelists.Count == 0)
            {
                shell.WriteLine(Loc.GetString("cmd-companywhitelistget-whitelisted-none", ("player", player)));
                return;
            }

            shell.WriteLine(Loc.GetString("cmd-companywhitelistget-whitelisted-for",
                ("player", player),
                ("companies", string.Join(", ", whitelists))));
            return;
        }

        shell.WriteError(Loc.GetString("cmd-companywhitelist-player-not-found", ("player", player)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                _players.Sessions.Select(s => s.Name),
                Loc.GetString("cmd-companywhitelist-hint-player"));
        }

        return CompletionResult.Empty;
    }
}

[AdminCommand(AdminFlags.Whitelist)]
public sealed class RemoveCompanyWhitelistCommand : LocalizedCommands
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly CompanyManager _company = default!;
    [Dependency] private readonly IPlayerLocator _playerLocator = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override string Command => "companywhitelistremove";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number-need-specific",
                ("properAmount", 2),
                ("currentAmount", args.Length)));
            shell.WriteLine(Help);
            return;
        }

        var player = args[0].Trim();
        var company = new ProtoId<CompanyPrototype>(args[1].Trim());
        if (!_prototypes.TryIndex(company, out var companyPrototype))
        {
            shell.WriteError(Loc.GetString("cmd-companywhitelist-company-does-not-exist", ("company", company)));
            shell.WriteLine(Help);
            return;
        }

        var data = await _playerLocator.LookupIdByNameAsync(player);
        if (data != null)
        {
            var guid = data.UserId;
            var isWhitelisted = _company.IsWhitelisted(guid, company);
            if (!isWhitelisted)
            {
                shell.WriteError(Loc.GetString("cmd-companywhitelistremove-was-not-whitelisted",
                    ("player", player),
                    ("companyId", company.Id),
                    ("companyName", companyPrototype.Name)));
                return;
            }

            _company.RemoveWhitelist(guid, company);
            shell.WriteLine(Loc.GetString("cmd-companywhitelistremove-removed",
                ("player", player),
                ("companyId", company.Id),
                ("companyName", companyPrototype.Name)));
            return;
        }

        shell.WriteError(Loc.GetString("cmd-companywhitelist-player-not-found", ("player", player)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                _players.Sessions.Select(s => s.Name),
                Loc.GetString("cmd-companywhitelist-hint-player"));
        }

        if (args.Length == 2)
        {
            return CompletionResult.FromHintOptions(
                _prototypes.EnumeratePrototypes<CompanyPrototype>()
                    .Where(p => p.Whitelisted)
                    .Select(p => p.ID),
                Loc.GetString("cmd-companywhitelist-hint-company"));
        }

        return CompletionResult.Empty;
    }
}
