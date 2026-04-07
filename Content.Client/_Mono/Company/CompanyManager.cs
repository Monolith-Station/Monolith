using Content.Shared._Mono.CCVar;
using Content.Shared._Mono.Company;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Client._Mono.Company;

public sealed partial class CompanyManager
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private HashSet<ProtoId<CompanyPrototype>> _whitelist = new();

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgCompanyWhitelist>(OnWhitelistMsg);
    }

    private void OnWhitelistMsg(MsgCompanyWhitelist msg)
    {
        _whitelist = msg.Whitelist;
    }

    public bool IsPlayerWhitelisted(ProtoId<CompanyPrototype> company)
    {
        return _whitelist.Contains(company);
    }

    public bool IsAllowed(ProtoId<CompanyPrototype> company)
    {
        if (!_config.GetCVar(MonoCVars.CompanyWhitelist))
            return true;

        if (!_proto.TryIndex(company, out var companyPrototype) ||
            !companyPrototype.Whitelisted)
        {
            return true;
        }

        return IsPlayerWhitelisted(company);
    }
}
