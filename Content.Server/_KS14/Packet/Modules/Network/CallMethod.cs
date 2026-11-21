using System.Threading.Tasks;
using Content.Server._KS14.Packet.States;

namespace Content.Server._KS14.Packet.Modules.Network;

[ModuleMethod("NetworkPacketModule")]
public sealed class CallMethod : ModuleMethod
{
    public override PacketModule? Module { get; set; }

    public override Action<int, string, DeviceCall> ModuleExec { get; }

    private void Func(int frequency, string address, DeviceCall call)
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

        module.PacketSystem.TryWrapSystemCall(() => call.UpdateCall(receiver, module.PacketSystem, module.EntityManager, module.PrototypeManager, module.Executor.Comp1));
    }

    public CallMethod(PacketModule? module) : base(module)
    {
        Id = "call";
        Module = module;
        ModuleExec = Func;
    }
}
