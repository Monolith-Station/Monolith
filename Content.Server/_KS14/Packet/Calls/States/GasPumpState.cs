using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Binary.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States;

public sealed class GasPressurePumpState : DeviceCall
{
    [PacketField(PacketFieldType.Returns)]
    public float Pressure { get; set; }
    [PacketField(PacketFieldType.Returns)]
    public bool Enabled { get; set; }
    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        if (!entityMan.TryGetComponent<GasPressurePumpComponent>(entity, out var pumpComponent))
            return;

        Pressure = pumpComponent.TargetPressure;
        Enabled = pumpComponent.Enabled;
    }
}

public sealed partial class GasVolumePumpState : DeviceCall
{
    [PacketField(PacketFieldType.Returns)]
    public float Volume { get; set; }
    [PacketField(PacketFieldType.Returns)]
    public bool Enabled { get; set; }
    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        if (!entityMan.TryGetComponent<GasVolumePumpComponent>(entity, out var pumpComponent))
            return;

        Volume = pumpComponent.TransferRate;
        Enabled = pumpComponent.Enabled;
    }
}
