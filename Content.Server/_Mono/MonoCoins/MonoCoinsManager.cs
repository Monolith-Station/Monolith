using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Mono.MonoCoins;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using System.Collections.Concurrent;

namespace Content.Server._Mono.MonoCoins;

/// <summary>
/// System that handles MonoCoins balance for players.
/// </summary>
public sealed partial class MonoCoinsManager
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private INetManager _net = default!;

    private readonly ConcurrentDictionary<NetUserId, long> _cachedBalance = new();

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgMonoCoins>();
        _net.RegisterNetMessage<MsgMonoCoinsRequest>(OnRequestBalance);
        _net.Connected += OnConnected;
    }

    /// <summary>
    /// Handles requests for MonoCoins balance from clients.
    /// </summary>
    private async void OnRequestBalance(MsgMonoCoinsRequest msg)
    {
        SendBalance(msg.MsgChannel);
    }

    private async void OnConnected(object? sender, NetChannelArgs e)
    {
        SendBalance(e.Channel);
    }

    private async void SendBalance(INetChannel player)
    {
        var balance = await GetMonoCoinsBalanceAsync(player.UserId);
        var msg = new MsgMonoCoins { Coins = balance };
        _net.ServerSendMessage(msg, player);
    }

    /// <summary>
    /// Gets the MonoCoins balance for a player from the cache or the database.
    /// </summary>
    /// <param name="userId">The player's UserId</param>
    /// <returns>The player's MonoCoins balance, or 0 if not found</returns>
    public async Task<long> GetMonoCoinsBalanceAsync(NetUserId userId)
    {
        long balance = -1;
        if (!_cachedBalance.TryGetValue(userId, out balance))
        {
            balance = await _db.GetMonoCoinsAsync(userId);
            _cachedBalance[userId] = balance;
        }
        return balance;
    }

    public long? GetMonoCoinsBalance(NetUserId userId)
    {
        _cachedBalance.TryGetValue(userId, out var balance);
        return balance;
    }

    /// <summary>
    /// Sets the MonoCoins balance for a player in the database.
    /// </summary>
    /// <param name="userId">The player's UserId</param>
    /// <param name="balance">The new balance</param>
    public async Task SetMonoCoinsBalanceAsync(NetUserId userId, long balance)
    {
        var wasBalance = await GetMonoCoinsBalanceAsync(userId);
        _cachedBalance[userId] = Math.Max(0L, balance);
        if (_player.TryGetSessionById(userId, out var session)) {
            SendBalance(session.Channel);
        }
        await _db.SetMonoCoinsAsync(userId, balance);
    }

    /// <summary>
    /// Adds MonoCoins to a player's balance in the database.
    /// </summary>
    /// <param name="userId">The player's UserId</param>
    /// <param name="amount">The amount to add</param>
    /// <returns>The new balance</returns>
    public async Task<long> AddMonoCoinsAsync(NetUserId userId, long amount)
    {
        var wasBalance = await GetMonoCoinsBalanceAsync(userId);
        _cachedBalance[userId] = Math.Max(0L, _cachedBalance[userId] + amount);
        if (_player.TryGetSessionById(userId, out var session)) {
            SendBalance(session.Channel);
        }
        return await _db.AddMonoCoinsAsync(userId, amount);
    }
}
