using Content.Server.Chat.Systems;
using Content.Shared._Goobstation.Dash;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Goobstation.Dash;

public sealed class DashActionChatSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DashActionEvent>(OnDash);
    }

    private void OnDash(DashActionEvent args)
    {
        if (args.Emote == null)
            return;

        if (!_prototype.TryIndex<EmotePrototype>(args.Emote.Value, out var emote))
            return;

        _chat.TryEmoteWithChat(
            args.Performer,
            emote,
            forceEmote: true);
    }
}