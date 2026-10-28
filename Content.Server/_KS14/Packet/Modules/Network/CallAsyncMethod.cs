using System.Threading.Tasks;
using Content.Server._KS14.Packet.States;

namespace Content.Server._KS14.Packet.Modules.Network;

[ModuleMethod("NetworkPacketModule")]
public sealed class CallAsyncMethod : ModuleMethod
{
    public override PacketModule? Module { get; set; }

    public override Func<int, string, DeviceCall, Task> ModuleExec { get; }

    private async Task Func(int frequency, string address, DeviceCall call)
    {
        if (Module is not NetworkPacketModule module)
            return;

        if (!module.PacketSystem.TryGetReceiver((module.Executor.Owner, module.Executor.Comp2),
                frequency,
                address,
                out var receiver))
        {
            module.Log(LogState.Error, "executor-log-no-device");
            return;
        }

        await module.PacketSystem.TryWrapSystemCallAsync(() => call.UpdateCall(receiver, module.PacketSystem, module.EntityManager, module.PrototypeManager, module.Executor.Comp1));
    }

    public CallAsyncMethod(PacketModule? module) : base(module)
    {
        Id = "call_async";
        Module = module;
        ModuleExec = Func;
    }
}
