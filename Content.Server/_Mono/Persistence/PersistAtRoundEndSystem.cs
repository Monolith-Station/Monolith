using Content.Server.Chat.Managers;
using Content.Shared._Mono.Persistence;
using Content.Shared.GameTicking;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server._Mono.Persistence;

public sealed partial class PersistAtRoundEndSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private PersistentProfileSystem _persistence = default!;
    [Dependency] private ISharedPlayerManager _players = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnded);
    }

    private void OnRoundEnded(RoundEndedEvent args)
    {
        var items = new Dictionary<EntityUid, List<EntityUid>>();
        var query = EntityQueryEnumerator<PersistAtRoundEndComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var transform))
        {
            if (!TryFindPlayer(transform.ParentUid, out var player))
                continue;

            if (!items.TryGetValue(player, out var playerItems))
                items[player] = playerItems = [];

            playerItems.Add(uid);
        }

        foreach (var (player, entities) in items)
        {
            if (_persistence.SaveRoundEndItems(player, entities) &&
                _players.TryGetSessionByEntity(player, out var session))
            {
                _chat.DispatchServerMessage(session, Loc.GetString("persistence-round-end-items-saved"));
            }
        }
    }

    private bool TryFindPlayer(EntityUid uid, out EntityUid player)
    {
        while (true)
        {
            if (_players.TryGetSessionByEntity(uid, out _))
            {
                player = uid;
                return true;
            }

            if (HasComp<MapComponent>(uid) || !TryComp(uid, out TransformComponent? transform))
            {
                player = default;
                return false;
            }

            uid = transform.ParentUid;
        }
    }
}
