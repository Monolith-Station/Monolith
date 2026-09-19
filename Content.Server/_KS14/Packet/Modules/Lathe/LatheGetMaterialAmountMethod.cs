using System.Threading.Tasks;
using Content.Shared.Materials;

namespace Content.Server._KS14.Packet.Modules.Lathe;

[ModuleMethod("LathePacketModule")]
public sealed class LatheGetMaterialAmountMethod : ModuleMethod
{
    public override PacketModule? Module { get; set; }
    public override Func<int, string, string, Task<int>> ModuleExec { get; }

    private async Task<int> Func(int frequency, string address, string material)
    {
        if (Module is not LathePacketModule module)
            return 0;

        var matSys = module.MaterialStorageSystem;
        var packetSys = module.PacketSystem;
        var protoMan = module.PrototypeManager;

        if (!protoMan.TryIndex<MaterialPrototype>(material, out var materialProto))
        {
            module.Log(LogState.Error, "executor-log-invalid-material");
            return 0;
        }

        if (!packetSys.TryGetReceiver((module.Executor.Owner, module.Executor.Comp2), frequency, address, out var receiver))
        {
            module.Log(LogState.Error, "executor-log-no-device");
            return 0;
        }

        return await packetSys.TryWrapSystemCall(() =>  matSys.GetMaterialAmount(receiver, materialProto),
            Channel);
    }

    public LatheGetMaterialAmountMethod(PacketModule? module) : base(module)
    {
        Id = "lathe_get_material_amount";
        Module = module;
        ModuleExec = Func;
    }
}
