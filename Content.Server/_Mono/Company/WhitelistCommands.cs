using Content.Server.Administration;
using Content.Server.Database;
using Content.Shared._Mono.Company;
using Content.Shared.Administration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server._Mono.Company;

[ToolshedCommand(Name = "companywhitelist"), AdminCommand(AdminFlags.Whitelist)]
public sealed class CompanyWhitelistCommand : ToolshedCommand
{
    [Dependency] private readonly CompanyManager _company = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IServerDbManager _db = default!;

    [CommandImplementation("add")]
    public async void Add(
        [CommandInvocationContext] IInvocationContext ctx,
        [CommandArgument] ICommonSession session,
        [CommandArgument] ProtoId<CompanyPrototype> company)
    {
        if (!_prototypes.TryIndex(company, out var companyPrototype))
        {
            ctx.ReportError(new NotAValidPrototype(company, nameof(CompanyPrototype)));
            return;
        }

        var guid = session.UserId;
        var isWhitelisted = _company.IsWhitelisted(guid, company);

        if (isWhitelisted)
        {
            ctx.WriteLine(Loc.GetString("cmd-companywhitelistadd-already-whitelisted",
                ("player", session.Name),
                ("companyId", company.Id),
                ("companyName", companyPrototype.Name)));
            return;
        }

        _company.AddWhitelist(guid, company);
        ctx.WriteLine(Loc.GetString("cmd-companywhitelistadd-added",
            ("player", session.Name),
            ("companyId", company.Id),
            ("companyName", companyPrototype.Name)));
    }

    [CommandImplementation("player")]
    public async void GetPlayerWhitelist(
        [CommandInvocationContext] IInvocationContext ctx,
        [CommandArgument] ICommonSession session)
    {
        var guid = session.UserId;
        var whitelists = await _db.GetPlayerCompanyWhitelists(guid);
        if (whitelists.Count == 0)
        {
            ctx.WriteLine(Loc.GetString("cmd-companywhitelistplayer-whitelisted-none", ("player", session.Name)));
            return;
        }

        ctx.WriteLine(Loc.GetString("cmd-companywhitelistplayer-whitelisted-for",
            ("player", session.Name),
            ("companies", string.Join(", ", whitelists))));
    }

    [CommandImplementation("players")]
    public async void GetCompanyWhitelist(
        [CommandInvocationContext] IInvocationContext ctx,
        [CommandArgument] ProtoId<CompanyPrototype> company)
    {
        if (!_prototypes.TryIndex(company, out var companyPrototype))
        {
            ctx.ReportError(new NotAValidPrototype(company, nameof(CompanyPrototype)));
            return;
        }

        var whitelisted = await _db.GetCompanyWhitelists(company);
        if (whitelisted.Count == 0)
        {
            ctx.WriteLine(Loc.GetString("cmd-companywhitelistplayers-whitelisted-none", ("company", company)));
            return;
        }

        ctx.WriteLine(Loc.GetString("cmd-companywhitelistplayers-whitelisted-for",
            ("company", company),
            ("players", string.Join(", ", whitelisted))));
    }

    [CommandImplementation("remove")]
    public async void Remove(
        [CommandInvocationContext] IInvocationContext ctx,
        [CommandArgument] ICommonSession session,
        [CommandArgument] ProtoId<CompanyPrototype> company)
    {
        if (!_prototypes.TryIndex(company, out var companyPrototype))
        {
            ctx.ReportError(new NotAValidPrototype(company, nameof(CompanyPrototype)));
            return;
        }

        var guid = session.UserId;
        var isWhitelisted = _company.IsWhitelisted(guid, company);

        if (!isWhitelisted)
        {
            ctx.WriteLine(Loc.GetString("cmd-companywhitelistremove-was-not-whitelisted",
                ("player", session.Name),
                ("companyId", company.Id),
                ("companyName", companyPrototype.Name)));
            return;
        }

        _company.RemoveWhitelist(guid, company);
        ctx.WriteLine(Loc.GetString("cmd-companywhitelistremove-removed",
            ("player", session.Name),
            ("companyId", company.Id),
            ("companyName", companyPrototype.Name)));
    }
}
