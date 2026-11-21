using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States.Calls;

public sealed class LathePrintCall : DeviceCall
{
    [PacketField(PacketFieldType.Accepts)]
    public string[] Recipes = [];

    [PacketField(PacketFieldType.Accepts)]
    public int[] Amount = [];

    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        for (var i = 0; i < Recipes.Length; i++)
        {
            var recipe = protoMan.Index<LatheRecipePrototype>(Recipes[i]);

            packetSystem.LatheSystem.TryAddToQueue(entity, recipe, Amount[i]);
        }

        packetSystem.LatheSystem.TryStartProducing(entity);
    }
}
