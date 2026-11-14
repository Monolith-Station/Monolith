using System.Linq;
using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Server.Ame.Components;
using Content.Server.Chemistry.Components;
using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.VendingMachines;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States;

public sealed class AmeState : DeviceCall
{
    [PacketField(PacketFieldType.Returns)]
    public bool Injecting { get; set; }
    [PacketField(PacketFieldType.Returns)]
    public int InjectionAmount { get; set; }
    [PacketField(PacketFieldType.Returns)]
    public float Stability { get; set; }

    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        if (!entityMan.TryGetComponent<AmeControllerComponent>(entity, out var ame))
            return;

        Injecting = ame.Injecting;
        InjectionAmount = ame.InjectionAmount;
        Stability = ame.Stability;
    }
}
