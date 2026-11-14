using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.Modules.Network;

public sealed class NetworkPacketModule : PacketModule
{
    public SharedDeviceLinkSystem DeviceLinkSystem;

    public NetworkPacketModule(EntityManager manager, PrototypeManager protoMan, PacketSystem packet) : base(manager, protoMan, packet)
    {
        DeviceLinkSystem = manager.System<SharedDeviceLinkSystem>();
    }
}
