using System.Threading.Channels;

namespace Content.Server._KS14.Packet.Modules;

/// <summary>
/// Used for properly loading methods into JINT engine, while also handling dependencies and async operations
/// </summary>
public abstract class ModuleMethod
{
    public abstract PacketModule? Module { get; set; }

    public string Id = "method";

    public abstract object ModuleExec { get; }

    public Channel<object> Channel { get; protected set; } = System.Threading.Channels.Channel.CreateUnbounded<object>();

    public ModuleMethod(PacketModule? module) { }
}
