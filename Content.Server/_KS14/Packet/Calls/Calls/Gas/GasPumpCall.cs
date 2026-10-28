using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Binary.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States;

public sealed class GasPumpCall : DeviceCall
{
    [PacketField(PacketFieldType.Accepts)]
    public float TransferRate { get; set; }

    [PacketField(PacketFieldType.Accepts)]
    public bool Enabled { get; set; }
    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        if (entityMan.TryGetComponent<GasPressurePumpComponent>(entity, out var pumpComponent))
        {
            pumpComponent.TargetPressure = TransferRate;
            pumpComponent.Enabled = Enabled;
        }
        else if (entityMan.TryGetComponent<GasVolumePumpComponent>(entity, out var volumePumpComponent))
        {
            volumePumpComponent.TransferRate = TransferRate;
            volumePumpComponent.Enabled = Enabled;
        }
    }
}
