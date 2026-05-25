using Content.Shared._Mono.MonoCoins;
using Robust.Shared.Network;

namespace Content.Client._Mono.MonoCoins;

/// <summary>
/// Client-side system for handling MonoCoins balance requests and responses.
/// </summary>
public sealed partial class MonoCoinsManager
{
    [Dependency] private INetManager _net = default!;

    /// <summary>
    /// The last known MonoCoins balance. -1 indicates balance hasn't been fetched yet.
    /// </summary>
    private long _lastKnownBalance = -1;

    /// <summary>
    /// Event raised when MonoCoins balance is updated.
    /// </summary>
    public event Action<long>? BalanceUpdated;

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgMonoCoins>(OnBalanceGet);
        _net.RegisterNetMessage<MsgMonoCoinsRequest>();
    }

    /// <summary>
    /// Handles MonoCoins balance response from server.
    /// </summary>
    private void OnBalanceGet(MsgMonoCoins msg)
    {
        _lastKnownBalance = msg.Coins;
        BalanceUpdated?.Invoke(_lastKnownBalance);
    }

    /// <summary>
    /// Requests the current MonoCoins balance from the server.
    /// </summary>
    public void RequestBalance()
    {
        var message = new MsgMonoCoinsRequest();
        _net.ClientSendMessage(message);
    }

    /// <summary>
    /// Gets the last known MonoCoins balance.
    /// Returns -1 if balance hasn't been fetched yet.
    /// </summary>
    public long GetLastKnownBalance()
    {
        return _lastKnownBalance;
    }
}
