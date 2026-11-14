using System.Threading;
using Content.Server._KS14.Packet.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States;

public abstract class DeviceCall
{
    public void UpdateCall(EntityUid entity,
        PacketSystem packetSystem,
        EntityManager entityManager,
        IPrototypeManager prototypeManager,
        PacketExecutorComponent executor)
    {
        GetCall(entity, packetSystem, entityManager, prototypeManager, executor);
    }

    protected abstract void GetCall(EntityUid entity,
        PacketSystem packetSystem,
        EntityManager entityManager,
        IPrototypeManager prototypeManager,
        PacketExecutorComponent executor);
}
