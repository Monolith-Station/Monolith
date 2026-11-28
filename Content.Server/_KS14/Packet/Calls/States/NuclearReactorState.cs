using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States;

public sealed class NuclearReactorState : DeviceCall
{
    [PacketField(PacketFieldType.Returns)]
    public float Temperature { get; set; }
    [PacketField(PacketFieldType.Returns)]
    public float RodInsertion { get; set; }

    [PacketField(PacketFieldType.Returns)]
    public float ThermalPower { get; set; }
    [PacketField(PacketFieldType.Returns)]
    public float EmittedRadiation { get; set; }

    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        if (!entityMan.TryGetComponent<NuclearReactorComponent>(entity, out var reactor))
            return;

        Temperature = reactor.Temperature;
        RodInsertion = reactor.ControlRodInsertion;
        ThermalPower = reactor.ThermalPower;
        EmittedRadiation = reactor.TotalRads;
    }
}
