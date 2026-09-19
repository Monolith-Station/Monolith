using System.Threading.Tasks;
using Content.Shared.Research.Prototypes;

namespace Content.Server._KS14.Packet.Modules.Lathe;

[ModuleMethod("LathePacketModule")]
public sealed class LathePrintMethod : ModuleMethod
{
    public override PacketModule? Module { get; set; }

    public override Func<int, string, string, int, Task> ModuleExec { get; }

    private async Task Func(int frequency, string address, string item, int quantity)
    {
        if (Module is not LathePacketModule module)
            return;

        var latheSys = module.LatheSystem;
        var protoMan = module.PrototypeManager;
        var packetSys = module.PacketSystem;

        if (!protoMan.TryIndex<LatheRecipePrototype>(item, out var recipe))
        {
            module.Log(LogState.Error, "executor-log-invalid-lathe-recipe");
            return;
        }

        if (!packetSys.TryGetReceiver((module.Executor.Owner, module.Executor.Comp2), frequency, address, out var receiver))
        {
            module.Log(LogState.Error, "executor-log-no-device");
            return;
        }

        packetSys.TryWrapSystemCall(() =>
        {
            latheSys.TryAddToQueue(receiver, recipe, quantity);
            latheSys.TryStartProducing(receiver);
        });

        await Task.Delay(recipe.CompleteTime * quantity);
    }

    public LathePrintMethod(PacketModule? module) : base(module)
    {
        Id = "lathe_print";
        Module = module;
        ModuleExec = Func;
    }
}
