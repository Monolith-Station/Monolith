using System.Linq;
using Content.Server.Administration.Managers;
using Content.Shared._Mono.Company;
using Content.Shared.Administration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server._Mono.Company;

[ToolshedCommand(Name = "company"), AnyCommand]
public sealed class CompanyCommand : ToolshedCommand
{
    [Dependency] private readonly CompanyManager _company = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IAdminManager _admin = default!;

    [CommandImplementation("addmember")]
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

        if (ctx.Session != null && !_admin.HasAdminFlag(ctx.Session, AdminFlags.Whitelist) && !_company.IsOwner(ctx.Session, company))
        {

            ctx.WriteLine(Loc.GetString("cmd-company-not-enough-permissions"));
            return;
        }

        var guid = session.UserId;
        var isWhitelisted = _company.IsMember(guid, company);

        if (isWhitelisted)
        {
            ctx.WriteLine(Loc.GetString("cmd-company-memberadd-already-whitelisted",
                ("player", session.Name),
                ("companyId", company.Id),
                ("companyName", companyPrototype.Name)));
            return;
        }

        _company.AddMember(guid, company);
        ctx.WriteLine(Loc.GetString("cmd-company-memberadd-added",
            ("player", session.Name),
            ("companyId", company.Id),
            ("companyName", companyPrototype.Name)));
    }

    [CommandImplementation("playercompanies")]
    public async void GetPlayerWhitelist(
        [CommandInvocationContext] IInvocationContext ctx,
        [CommandArgument] ICommonSession session)
    {
        if (ctx.Session != null && !_admin.HasAdminFlag(ctx.Session, AdminFlags.Whitelist))
        {

            ctx.WriteLine(Loc.GetString("cmd-company-not-enough-permissions"));
            return;
        }

        var guid = session.UserId;
        var whitelists = _company.GetPlayerCompaniesCached(guid);
        if (whitelists.Count == 0)
        {
            ctx.WriteLine(Loc.GetString("cmd-company-playercompanies-whitelisted-none", ("player", session.Name)));
            return;
        }

        ctx.WriteLine(Loc.GetString("cmd-company-playercompanies-whitelisted-for",
            ("player", session.Name),
            ("companies", string.Join(", ", whitelists))));
    }

    [CommandImplementation("members")]
    public async void GetCompanyWhitelist(
        [CommandInvocationContext] IInvocationContext ctx,
        [CommandArgument] ProtoId<CompanyPrototype> company)
    {
        if (!_prototypes.TryIndex(company, out var companyPrototype))
        {
            ctx.ReportError(new NotAValidPrototype(company, nameof(CompanyPrototype)));
            return;
        }

        if (ctx.Session != null && !_admin.HasAdminFlag(ctx.Session, AdminFlags.Whitelist) && !_company.IsOwner(ctx.Session, company))
        {

            ctx.WriteLine(Loc.GetString("cmd-company-not-enough-permissions"));
            return;
        }

        var whitelisted = _company.GetCompanyMembersCached(company);
        if (whitelisted.Count == 0)
        {
            ctx.WriteLine(Loc.GetString("cmd-company-members-whitelisted-none", ("company", company)));
            return;
        }

        var members = whitelisted.Where(w => !w.Owner).Select(m => m.LastSeenUserName);
        var owners = whitelisted.Where(w => w.Owner).Select(m => m.LastSeenUserName);

        ctx.WriteLine(Loc.GetString("cmd-company-members-whitelisted-for",
            ("company", company),
            ("players", string.Join(", ", members)),
            ("owners", string.Join(", ", owners))));
    }

    [CommandImplementation("setowner")]
    public async void SetOwner(
        [CommandInvocationContext] IInvocationContext ctx,
        [CommandArgument] ProtoId<CompanyPrototype> company,
        [CommandArgument] ICommonSession session,
        [CommandArgument] bool owner
    )
    {
        if (!_prototypes.TryIndex(company, out var companyPrototype))
        {
            ctx.ReportError(new NotAValidPrototype(company, nameof(CompanyPrototype)));
            return;
        }

        if (ctx.Session != null && !_admin.HasAdminFlag(ctx.Session, AdminFlags.Whitelist))
        {

            ctx.WriteLine(Loc.GetString("cmd-company-not-enough-permissions"));
            return;
        }

        _company.SetOwner(company, session, owner);
        ctx.WriteLine(Loc.GetString("cmd-company-setowner-success", ("player", session.Name), ("status", owner)));
    }

    [CommandImplementation("removemember")]
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

        if (ctx.Session != null && !_admin.HasAdminFlag(ctx.Session, AdminFlags.Whitelist) && !_company.IsOwner(ctx.Session, company))
        {

            ctx.WriteLine(Loc.GetString("cmd-company-not-enough-permissions"));
            return;
        }

        var guid = session.UserId;
        var isWhitelisted = _company.IsMember(guid, company);

        if (!isWhitelisted)
        {
            ctx.WriteLine(Loc.GetString("cmd-company-memberremove-was-not-whitelisted",
                ("player", session.Name),
                ("companyId", company.Id),
                ("companyName", companyPrototype.Name)));
            return;
        }

        await _company.RemoveMember(guid, company);
        ctx.WriteLine(Loc.GetString("cmd-company-memberremove-removed",
            ("player", session.Name),
            ("companyId", company.Id),
            ("companyName", companyPrototype.Name)));
    }
}
