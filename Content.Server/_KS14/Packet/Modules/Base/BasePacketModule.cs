using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.Modules.Base;

/// <summary>
/// Basic firmware of every executor.
/// Contains critically important methods.
/// </summary>
public sealed class BasePacketModule(EntityManager manager, PrototypeManager protoMan, PacketSystem packet)
    : PacketModule(manager, protoMan, packet);
