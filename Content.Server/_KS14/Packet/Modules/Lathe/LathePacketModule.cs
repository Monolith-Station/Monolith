using Content.Server.Lathe;
using Content.Server.Materials;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.Modules.Lathe;

public sealed class LathePacketModule : PacketModule
{
    public LatheSystem LatheSystem;
    public MaterialStorageSystem MaterialStorageSystem;

    public LathePacketModule(EntityManager manager, PrototypeManager protoMan, PacketSystem packet) : base(manager, protoMan, packet)
    {
        LatheSystem = manager.System<LatheSystem>();
        MaterialStorageSystem = manager.System<MaterialStorageSystem>();
    }
}
