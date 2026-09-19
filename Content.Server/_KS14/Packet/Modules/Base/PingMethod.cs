using System.Threading.Tasks;

namespace Content.Server._KS14.Packet.Modules.Base;

/// <summary>
/// Upon execution - tries to get random receiver in range of 10 meters, then returns it's address.
/// Can be used to either detect addresses or bruteforce frequencies.
/// </summary>
[ModuleMethod("BasePacketModule")]
public sealed class PingMethod : ModuleMethod
{
    public override PacketModule? Module { get; set; }
    public override Func<int, Task<string>> ModuleExec { get; }

    private async Task<string> Func(int frequency)
    {
        if (Module is not BasePacketModule module)
            return "";

        var packetSys = module.PacketSystem;

        return await packetSys.TryWrapSystemCall(() => packetSys.TryRandomReceiver(module.Executor, frequency, 10, out var receiver) ? receiver.Comp.Address : "",
            Channel);
    }

    public PingMethod(PacketModule? module) : base(module)
    {
        Id = "ping";
        Module = module;
        ModuleExec = Func;
    }
}
