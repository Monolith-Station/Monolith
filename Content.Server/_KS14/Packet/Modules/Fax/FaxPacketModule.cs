using Content.Server.Fax;
using Content.Server.Lathe;
using Content.Server.Materials;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.Modules.Fax;

public sealed class FaxPacketModule : PacketModule
{
    public FaxSystem FaxSystem;

    public FaxPacketModule(EntityManager manager, PrototypeManager protoMan, PacketSystem packet) : base(manager, protoMan, packet)
    {
        FaxSystem = manager.System<FaxSystem>();
    }
}
