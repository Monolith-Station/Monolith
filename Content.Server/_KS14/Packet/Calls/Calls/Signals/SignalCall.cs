using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Server.Lathe;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States.Calls;

public sealed class SignalCall : DeviceCall
{
    [PacketField(PacketFieldType.Accepts)]
    public string PortName = "";

    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        var eventArgs = new SignalReceivedEvent(PortName); // This should have used method, but due to DeviceLinkSystem requiring source, it is not possible.
        entityMan.EventBus.RaiseLocalEvent(entity, ref eventArgs);
    }
}
