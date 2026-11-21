using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Server.Lathe;
using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Content.Shared.VendingMachines;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States.Calls;

public sealed class NuclearReactorControlCall : DeviceCall
{
    [PacketField(PacketFieldType.Accepts)]
    public float RodInsertion { get; set; }

    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        if (!entityMan.TryGetComponent<NuclearReactorComponent>(entity, out var reactor))
            return;

        reactor.ControlRodInsertion = RodInsertion;
    }
}
