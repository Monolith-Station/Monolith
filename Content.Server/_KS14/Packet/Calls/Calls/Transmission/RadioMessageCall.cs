using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Server.Ame.Components;
using Content.Server.Lathe;
using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Content.Shared.Materials;
using Content.Shared.Radio.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.VendingMachines;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States.Calls;

public sealed class RadioMessageCall : DeviceCall
{
    [PacketField(PacketFieldType.Accepts)]
    public string Message = "This is a test message.";
    [PacketField(PacketFieldType.Accepts)]
    public string Channel = "Common";

    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        if (!entityMan.HasComponent<RadioSpeakerComponent>(entity))
            return;

        packetSystem.RadioSystem.SendRadioMessage(entity, Channel, Message, entity);
    }
}
