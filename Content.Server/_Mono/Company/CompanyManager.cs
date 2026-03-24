using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Mono.CCVar;
using Content.Shared._Mono.Company;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Company;

public sealed class CompanyManager : IPostInjectInit
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly UserDbDataManager _userDb = default!;
    [Dependency] private readonly ILogManager _log = default!;

    private readonly ISawmill _sawmill = default!;

    private readonly Dictionary<NetUserId, HashSet<string>> _whitelists = new();

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgCompanyWhitelist>();

        _log.GetSawmill(nameof(CompanyManager));
    }

    private async Task LoadData(ICommonSession session, CancellationToken cancel)
    {
        var whitelists = await _db.GetCompanyWhitelists(session.UserId, cancel);
        cancel.ThrowIfCancellationRequested();
        _whitelists[session.UserId] = whitelists.ToHashSet();
    }

    private void FinishLoad(ICommonSession session)
    {
        SendCompanyWhitelist(session);
    }

    private void ClientDisconnected(ICommonSession session)
    {
        _whitelists.Remove(session.UserId);
    }

    public async void AddWhitelist(NetUserId player, ProtoId<CompanyPrototype> company)
    {
        if (_whitelists.TryGetValue(player, out var whitelists))
            whitelists.Add(company);

        await _db.AddCompanyWhitelist(player, company);

        if (_player.TryGetSessionById(player, out var session))
            SendCompanyWhitelist(session);
    }

    public bool IsAllowed(ICommonSession session, ProtoId<CompanyPrototype> company)
    {
        if (!_config.GetCVar(MonoCVars.CompanyWhitelist))
            return true;

        if (!_prototypes.TryIndex(company, out var companyPrototype) ||
            !companyPrototype.Whitelisted)
        {
            return true;
        }

        return IsWhitelisted(session.UserId, company);
    }

    public bool IsWhitelisted(NetUserId player, ProtoId<CompanyPrototype> company)
    {
        if (!_whitelists.TryGetValue(player, out var whitelists))
        {
            _sawmill.Error($"Unable to check if player {player} is whitelisted for {company}. Stack trace:\\n{Environment.StackTrace}");
            return false;
        }

        return whitelists.Contains(company);
    }

    public async void RemoveWhitelist(NetUserId player, ProtoId<CompanyPrototype> company)
    {
        _whitelists.GetValueOrDefault(player)?.Remove(company);
        await _db.RemoveCompanyWhitelist(player, company);

        if (_player.TryGetSessionById(new NetUserId(player), out var session))
            SendCompanyWhitelist(session);
    }

    public void SendCompanyWhitelist(ICommonSession player)
    {
        var msg = new MsgCompanyWhitelist
        {
            Whitelist = _whitelists.GetValueOrDefault(player.UserId) ?? new HashSet<string>()
        };

        _net.ServerSendMessage(msg, player.Channel);
    }

    void IPostInjectInit.PostInject()
    {
        _userDb.AddOnLoadPlayer(LoadData);
        _userDb.AddOnFinishLoad(FinishLoad);
        _userDb.AddOnPlayerDisconnect(ClientDisconnected);
    }
}
