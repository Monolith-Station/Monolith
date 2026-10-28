using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Server.Chemistry.Components;
using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States;

public sealed class SolutionState : DeviceCall
{
    [PacketField(PacketFieldType.Returns)]
    public ProtoId<ReagentPrototype>[] Reagents = [];
    [PacketField(PacketFieldType.Returns)]
    public float[] Quantity = [];

    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        if (!entityMan.TryGetComponent<SolutionComponent>(entity, out var solution))
            return;

        for (var i = 0; i < solution.Solution.Contents.Count; i++)
        {
            Quantity[i] = solution.Solution.Contents[i].Quantity.Float();
            Reagents[i] = solution.Solution.Contents[i].Reagent.Prototype;
        }
    }
}
