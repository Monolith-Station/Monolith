using System.Threading.Tasks;
using Content.Shared.Research.Prototypes;

namespace Content.Server._KS14.Packet.Modules.Lathe;

[ModuleMethod("LathePacketModule")]
public sealed class LatheCanPrintMethod : ModuleMethod
{
    public override PacketModule? Module { get; set; }
    public override Func<int, string, string, int, Task<bool>> ModuleExec { get; }

    private async Task<bool> Func(int frequency, string address, string item, int quantity)
    {
        if (Module is not LathePacketModule module)
            return false;

        var latheSys = module.LatheSystem;
        var protoMan = module.PrototypeManager;
        var packetSys = module.PacketSystem;

        if (!protoMan.TryIndex<LatheRecipePrototype>(item, out var recipe))
        {
            module.Log(LogState.Error, "executor-log-invalid-lathe-recipe");
            return false;
        }

        if (!packetSys.TryGetReceiver((module.Executor.Owner, module.Executor.Comp2), frequency, address, out var receiver))
        {
            module.Log(LogState.Error, "executor-log-no-device");
            return false;
        }

        return await packetSys.TryWrapSystemCall(() =>  latheSys.CanProduce(receiver, recipe, quantity),
            Channel);
    }

    public LatheCanPrintMethod(PacketModule? module) : base(module)
    {
        Id = "lathe_can_print";
        Module = module;
        ModuleExec = Func;
    }
}
