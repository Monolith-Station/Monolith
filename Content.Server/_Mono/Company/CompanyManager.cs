using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Mono.CCVar;
using Content.Shared._Mono.Company;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Mono.Company;

public sealed class CompanyManager
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ILogManager _log = default!;

    private ISawmill _sawmill = default!;

    private readonly Dictionary<ProtoId<CompanyPrototype>, HashSet<CompanyMemberRecord>> _companies = new();

    public void Initialize()
    {
        _sawmill = _log.GetSawmill("company.manager");

        _net.RegisterNetMessage<MsgCompanyWhitelist>();
        _net.Connected += OnConnected;

        _ = LoadCompaniesData();
    }

    private async Task LoadCompaniesData()
    {
        var sw = new Stopwatch();
        sw.Start();

        var members = await _db.GetAllCompanyMembers();
        foreach (var proto in _proto.EnumeratePrototypes<CompanyPrototype>())
        {
            _companies.Add(proto.ID, new());
            _companies[proto.ID].UnionWith(members.Where(m => m.Company == proto.ID).ToHashSet());
        }

        _sawmill.Info($"All company members data loaded in {sw.Elapsed.TotalSeconds:.2}s");
    }

    private void OnConnected(object? sender, NetChannelArgs e)
    {
        SendCompanyWhitelist(e.Channel);
    }

    private async Task<HashSet<CompanyMemberRecord>> QueryCompanyMembers(ProtoId<CompanyPrototype> company)
    {
        return (await _db.GetCompanyMembers(company)).ToHashSet();
    }

    public HashSet<CompanyMemberRecord> GetCompanyMembers(ProtoId<CompanyPrototype> company)
    {
        if (!_companies.TryGetValue(company, out var members))
            return new();
        return members;
    }

    public HashSet<CompanyMemberRecord> GetAllCompanyMembers()
    {
        return _companies.Values.SelectMany(v => v).ToHashSet();
    }

    public CompanyMemberRecord? GetCompanyMember(ProtoId<CompanyPrototype> company, NetUserId player)
    {
        if (!_companies.TryGetValue(company, out var members))
            return null;

        return members.FirstOrNull(m => m.PlayerUserId == player);
    }

    public async void AddMember(NetUserId player, ProtoId<CompanyPrototype> company)
    {
        await _db.AddCompanyMember(player, company);

        var member = await _db.GetCompanyMember(company, player);
        if (member != null)
            _companies[company].Add(member.Value);

        if (_player.TryGetSessionById(player, out var session))
            SendCompanyWhitelist(session.Channel);
    }

    public bool IsAllowed(ICommonSession session, ProtoId<CompanyPrototype> company)
    {
        if (!_config.GetCVar(MonoCVars.CompanyWhitelist))
            return true;

        if (!_proto.TryIndex(company, out var companyPrototype) ||
            !companyPrototype.Whitelisted)
        {
            return true;
        }

        return IsMember(session.UserId, company);
    }

    public bool IsOwner(ICommonSession session, ProtoId<CompanyPrototype> company)
    {
        if (!_companies.TryGetValue(company, out var members))
            return false;

        var member = members.FirstOrNull(m => m.PlayerUserId == session.UserId);

        return member != null && member.Value.Owner;
    }

    public bool SetOwner(ProtoId<CompanyPrototype> company, NetUserId userId, bool owner)
    {
        var cached = GetCompanyMember(company, userId);

        if (cached is not { } member)
            return false;

        if (owner == member.Owner)
            return true;

        _db.SetCompanyOwner(company, userId, owner);

        _companies[company].RemoveWhere(w => w.PlayerUserId == userId);
        member.Owner = owner; // company member is struct so we got a copy here
        _companies[company].Add(member);
        return true;
    }

    public bool IsMember(NetUserId player, ProtoId<CompanyPrototype> company)
    {
        var member = GetCompanyMember(company, player);
        return member != null;
    }

    public async Task RemoveMember(NetUserId player, ProtoId<CompanyPrototype> company)
    {
        _companies[company].RemoveWhere(w => w.PlayerUserId == player);

        await _db.RemoveCompanyMember(player, company);

        if (_player.TryGetSessionById(player, out var session))
            SendCompanyWhitelist(session.Channel);
    }

    public HashSet<ProtoId<CompanyPrototype>> GetPlayerCompanies(NetUserId player)
    {
        var res = new HashSet<ProtoId<CompanyPrototype>>();

        foreach (var (key, members) in _companies)
        {
            var member = members.FirstOrNull(m => m.PlayerUserId == player);
            if (member != null)
                res.Add(key);
        }

        return res;
    }

    public void SendCompanyWhitelist(INetChannel player)
    {
        var msg = new MsgCompanyWhitelist
        {
            Whitelist = GetPlayerCompanies(player.UserId)
        };

        _net.ServerSendMessage(msg, player);
    }
}
