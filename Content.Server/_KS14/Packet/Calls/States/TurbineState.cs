using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States;

public sealed class TurbineState : DeviceCall
{
    [PacketField(PacketFieldType.Returns)]
    public float StatorLoad { get; set; }
    [PacketField(PacketFieldType.Returns)]
    public float FlowRate { get; set; }
    [PacketField(PacketFieldType.Returns)]
    public float Rpm { get; set; }

    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        if (!entityMan.TryGetComponent<TurbineComponent>(entity, out var reactor))
            return;

        StatorLoad = reactor.StatorLoad;
        FlowRate = reactor.FlowRate;
        Rpm = reactor.RPM;
    }
}
