using System.Threading.Tasks;

namespace Content.Server._KS14.Packet.Modules.Base;

/// <summary>
/// Awaits "SEND" command from executor.
/// Use this only for direct input from executor. In any other cases - use data methods.
/// </summary>
[ModuleMethod("BasePacketModule")]
public sealed class InputMethod : ModuleMethod
{
    public override PacketModule? Module { get; set; }

    public override Func<Task<string>> ModuleExec { get; }

    private async Task<string> Func()
    {
        return (string) await Channel.Reader.ReadAsync();
    }

    public InputMethod(PacketModule? module) : base(module)
    {
        Id = "input";
        Module = module;
        ModuleExec = Func;
    }
}
