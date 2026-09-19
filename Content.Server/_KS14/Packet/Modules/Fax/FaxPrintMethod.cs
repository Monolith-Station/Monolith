using Content.Shared.Fax;
using Content.Shared.Fax.Components;

namespace Content.Server._KS14.Packet.Modules.Fax;

[ModuleMethod("FaxPacketModule")]
public sealed class FaxPrintMethod : ModuleMethod
{
    public override PacketModule? Module { get; set; }
    public override Action<int, string, string> ModuleExec { get; }

    private void Func(int frequency, string address, string content)
    {
        if (Module is not FaxPacketModule module)
            return;

        var packetSys = module.PacketSystem;
        var faxSys = module.FaxSystem;

        if (!packetSys.TryGetReceiver((module.Executor.Owner, module.Executor.Comp2), frequency, address, out var receiver))
        {
            module.Log(LogState.Error, "executor-log-no-device");
            return;
        }

        packetSys.TryWrapSystemCall(() =>
        {
            if (!module.EntityManager.TryGetComponent<FaxMachineComponent>(receiver, out var faxMachine))
                return;

            faxSys.PrintFile(receiver, faxMachine, new FaxFileMessage(null,  content, false));
        });
    }

    public FaxPrintMethod(PacketModule? module) : base(module)
    {
        Id = "fax_print";
        Module = module;
        ModuleExec = Func;
    }
}
