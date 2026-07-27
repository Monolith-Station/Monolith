using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Speech;
using Content.Server.Speech.Components;
using Content.Shared._RMC14.Telephone;
using Content.Shared.Chat;
using Robust.Server.Audio;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.Telephone;

public sealed partial class RMCTelephoneSystem : SharedRMCTelephoneSystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IChatManager _chatManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCTelephoneComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<RMCTelephoneRingEvent>(OnTelephoneRing);
    }

    protected override void PickupPhone(Entity<RotaryPhoneComponent> rotary, EntityUid telephone, EntityUid user)
    {
        base.PickupPhone(rotary, telephone, user);
        EnsureComp<ActiveListenerComponent>(telephone);
    }

    private void OnTelephoneRing(ref RMCTelephoneRingEvent ev)
    {
        var key = HasComp<RotaryPhoneBackpackComponent>(ev.Receiving) ? "rmc-phone-ring-backpack" : "rmc-phone-ring";
        _chat.TrySendInGameICMessage(ev.Receiving, Loc.GetString(key), InGameICChatType.Emote, false, ignoreActionBlocker: true);

        if (TryComp<RotaryPhoneComponent>(ev.Receiving, out var phone) && phone.NotifyAdmins)
        {
            _chatManager.SendAdminAnnouncement(Loc.GetString("admin-call-incoming",
                ("actor", Name(ev.Actor)),
                ("from", Name(ev.Calling)),
                ("to", Name(ev.Receiving))));
        }
    }

    public void OnListen(Entity<RMCTelephoneComponent> ent, ref ListenEvent args)
    {
        OnListen(ent, args.Source, args.Message);
    }

    public void OnListen(Entity<RMCTelephoneComponent> ent, EntityUid source, string message)
    {
        if (HasComp<RMCTelephoneComponent>(source))
            return;

        if (!_hands.IsHolding(source, ent))
            return;

        if (ent.Comp.RotaryPhone is not { } rotary ||
            !TryGetOtherPhone(rotary, out var otherPhone) ||
            !TryGetPhoneHandHolder(otherPhone, out var holder) ||
            !TryComp(holder, out ActorComponent? actor))
        {
            return;
        }

        var name = GetPhoneName(rotary);
        var escapedMessage = FormattedMessage.EscapeText(message);
        var fullMessage = Loc.GetString("rmc-phone-speak", ("name", name), ("message", escapedMessage));
        var sound = _audio.GetSound(ent.Comp.SpeakSound);
        _chatManager.ChatMessageToOne(ChatChannel.Local, fullMessage, fullMessage, otherPhone, false, actor.PlayerSession.Channel, Color.FromHex("#9956D3"), true, sound, -12);
    }
}
