using Content.Shared.Preferences;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.GameTicking.Events;

public sealed class PlayerReadyRequestEvent(ICommonSession player, bool ready, Action complete) : EntityEventArgs
{
    public ICommonSession Player { get; } = player;
    public bool Ready { get; } = ready;
    public Action Complete { get; } = complete;
    public bool Deferred;
}

public sealed class RoundStartSpawnSelectionEvent(
    List<ICommonSession> players, Dictionary<NetUserId, HumanoidCharacterProfile> profiles) : EntityEventArgs
{
    public List<ICommonSession> Players { get; } = players;
    public Dictionary<NetUserId, HumanoidCharacterProfile> Profiles { get; } = profiles;
}

public sealed class PlayerLateJoinRequestEvent(
    ICommonSession player, EntityUid station, string? job, Action complete) : EntityEventArgs
{
    public ICommonSession Player { get; } = player;
    public EntityUid Station { get; } = station;
    public string? Job { get; } = job;
    public Action Complete { get; } = complete;
    public bool Deferred;
}
