using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Server.Lathe;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Content.Shared.VendingMachines;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States.Calls;

public sealed class VendingMachineExtractCall : DeviceCall
{
    [PacketField(PacketFieldType.Accepts)]
    public string Item = "";

    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        packetSystem.VendingMachineSystem.TryEjectVendorItem(entity, InventoryType.Regular, Item, false);
    }
}
