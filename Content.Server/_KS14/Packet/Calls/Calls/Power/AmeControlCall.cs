using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Server.Ame.Components;
using Content.Server.Lathe;
using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Content.Shared.VendingMachines;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States.Calls;

public sealed class AmeControlCall : DeviceCall
{
    [PacketField(PacketFieldType.Accepts)]
    public int? Injection { get; set; }
    [PacketField(PacketFieldType.Accepts)]
    public bool? Injecting { get; set; }

    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        if (!entityMan.TryGetComponent<AmeControllerComponent>(entity, out var ameControllerComponent))
            return;

        packetSystem.AmeControllerSystem.SetInjectionAmount(entity, Injection ?? ameControllerComponent.InjectionAmount);
        packetSystem.AmeControllerSystem.SetInjecting(entity, Injecting ?? ameControllerComponent.Injecting);
    }
}
