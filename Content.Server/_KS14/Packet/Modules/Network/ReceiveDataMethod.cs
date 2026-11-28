using System.Threading.Tasks;

namespace Content.Server._KS14.Packet.Modules.Network;

[ModuleMethod("NetworkPacketModule")]
public sealed class ReceiveDataMethod : ModuleMethod
{
    public override PacketModule? Module { get; set; }

    public override Func<Task<object>> ModuleExec { get; }

    private async Task<object> Func()
    {
        return await Channel.Reader.ReadAsync();
    }

    public ReceiveDataMethod(PacketModule? module) : base(module)
    {
        Id = "data_receive";
        Module = module;
        ModuleExec = Func;
    }
}
