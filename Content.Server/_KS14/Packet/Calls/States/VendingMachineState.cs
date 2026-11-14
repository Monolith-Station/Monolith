using System.Linq;
using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Server.Chemistry.Components;
using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.VendingMachines;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States;

public sealed class VendingMachineState : DeviceCall
{
    [PacketField(PacketFieldType.Returns)]
    public string[] Items = [];
    [PacketField(PacketFieldType.Returns)]
    public int[] Amount = [];

    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        if (!entityMan.TryGetComponent<VendingMachineComponent>(entity, out var vend))
            return;

        for (var i = 0; i < vend.Inventory.Count; i++)
        {
            Items[i] = vend.Inventory.Values.ToArray()[i].ID;
            Amount[i] = (int) vend.Inventory.Values.ToArray()[i].Amount;
        }
    }
}
