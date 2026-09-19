using System.Threading.Tasks;

namespace Content.Server._KS14.Packet.Modules.Base;

[ModuleMethod("BasePacketModule")]
public sealed class SleepMethod : ModuleMethod
{
    public override PacketModule? Module { get; set; }

    public override Func<int, Task> ModuleExec { get; }

    private async Task Func(int ms)
    {
        await Task.Delay(ms);
    }

    public SleepMethod(PacketModule? module) : base(module)
    {
        Id = "sleep";
        Module = module;
        ModuleExec = Func;
    }
}
