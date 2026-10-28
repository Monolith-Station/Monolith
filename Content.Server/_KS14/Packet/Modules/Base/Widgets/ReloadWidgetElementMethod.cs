using System.Threading.Tasks;
using Content.Shared._KS14.Packets.BUI;
using Content.Shared._KS14.Packets.Widgets;

namespace Content.Server._KS14.Packet.Modules.Base;

[ModuleMethod("BasePacketModule")]
public sealed class ReloadWidgetElementMethod : ModuleMethod
{
    public override PacketModule? Module { get; set; }

    public override Action<WidgetElement> ModuleExec { get; }

    private void Func(WidgetElement element)
    {
        if (Module is not {  } module)
            return;

        module.PacketSystem.TryWrapSystemCall(() => module.PacketSystem.UserInterfaceSystem.ServerSendUiMessage(
            module.Executor.Owner,
            ExecutorUiKey.Key,
            new WidgetUpdateElementMessage(element)));
    }

    public ReloadWidgetElementMethod(PacketModule? module) : base(module)
    {
        Id = "update_element";
        Module = module;
        ModuleExec = Func;
    }
}
