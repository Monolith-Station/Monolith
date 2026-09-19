using System.Threading.Tasks;
using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.Modules.Lathe;

[ModuleMethod("LathePacketModule")]
public sealed class LatheGetStoredMaterials : ModuleMethod
{
    public override PacketModule? Module { get; set; }
    public override Func<int, string, Task<Dictionary<ProtoId<MaterialPrototype>, int>>> ModuleExec { get; }

    private async Task<Dictionary<ProtoId<MaterialPrototype>, int>> Func(int frequency, string address)
    {
        if (Module is not LathePacketModule module)
            return [];

        var matSys = module.MaterialStorageSystem;
        var packetSys = module.PacketSystem;

        if (!packetSys.TryGetReceiver((module.Executor.Owner, module.Executor.Comp2),
                frequency,
                address,
                out var receiver))
        {
            module.Log(LogState.Error, "executor-log-no-device");
            return [];
        }

        return await packetSys.TryWrapSystemCall(() => matSys.GetStoredMaterials(receiver.Owner),
            Channel);
    }

    public LatheGetStoredMaterials(PacketModule? module) : base(module)
    {
        Id = "lathe_get_stored_materials";
        Module = module;
        ModuleExec = Func;
    }
}
