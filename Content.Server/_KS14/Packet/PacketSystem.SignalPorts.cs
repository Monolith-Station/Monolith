using Content.Server._KS14.Packet.Components;
using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet;

/// <summary>
/// This system is used for handling executor's ports to execute methods
/// </summary>
public sealed partial class PacketSystem
{
    /// <summary>
    /// Initializes ports for executor based on port naming and count.
    /// </summary>
    /// <param name="ent"></param>
    private void InitializePorts(Entity<PacketExecutorComponent> ent)
    {
        List<ProtoId<SinkPortPrototype>> ports = [];

        for (var i = 0; i < ent.Comp.PortCount; i++)
        {
            if (!_prototypeManager.TryIndex<SinkPortPrototype>(ent.Comp.SignalPortNaming + i, out var port))
                continue;

            ports.Add(port);
        }

        _deviceLinkSystem.EnsureSinkPorts(ent, ports.ToArray());
    }

    /// <summary>
    /// Links function to sink port.
    /// </summary>
    /// <param name="port"></param>
    /// <param name="funName"></param>
    /// <param name="ent"></param>
    public void RegisterSignalMethod(ProtoId<SinkPortPrototype> port, string funName, Entity<PacketExecutorComponent> ent)
    {
        var engine = EnsureEngine(ent);
        var value = engine.GetValue(funName);

        ent.Comp.ListeningPorts.TryAdd(port, value);
    }

    /// <summary>
    /// Activates function on signal (if any)
    /// </summary>
    /// <param name="port"></param>
    /// <param name="ent"></param>
    private void OnSignal(ProtoId<SinkPortPrototype> port, Entity<PacketExecutorComponent> ent)
    {
        if (!ent.Comp.ListeningPorts.TryGetValue(port, out var func))
            return;

        var engine = EnsureEngine(ent);
        engine.Invoke(func);
    }
}
