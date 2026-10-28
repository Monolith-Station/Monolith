using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Server.Lathe;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States.Calls;

public sealed class LatheExtractCall : DeviceCall
{
    [PacketField(PacketFieldType.Accepts)]
    public string[] Materials = [];

    [PacketField(PacketFieldType.Accepts)]
    public int[] Amount = [];

    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        for (var i = 0; i < Materials.Length; i++)
        {
            packetSystem.MaterialStorageSystem.EjectMaterial(entity, Materials[i], Amount[i]);
        }
    }
}
