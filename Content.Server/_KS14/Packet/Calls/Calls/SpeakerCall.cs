using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Server.Lathe;
using Content.Shared.Chat;
using Content.Shared.Fax;
using Content.Shared.Fax.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States.Calls;

public sealed class SpeakerCall : DeviceCall
{
    [PacketField(PacketFieldType.Accepts)]
    public string Text = "";

    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        if (!entityMan.HasComponent<SpeechComponent>(entity))
            return;

        packetSystem.ChatSystem.TrySendInGameICMessage(entity, Text, InGameICChatType.Speak, ChatTransmitRange.Normal);
    }
}
