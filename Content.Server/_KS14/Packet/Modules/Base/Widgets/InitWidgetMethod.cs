using System.Threading.Tasks;
using Content.Shared._KS14.Packets.BUI;
using Content.Shared._KS14.Packets.Widgets;

namespace Content.Server._KS14.Packet.Modules.Base;

[ModuleMethod("BasePacketModule")]
public sealed class InitWidgetMethod : ModuleMethod
{
    public override PacketModule? Module { get; set; }

    public override Action<Widget> ModuleExec { get; }

    private void Func(Widget widget)
    {
        if (Module is not {  } module)
            return;

        if (module.Executor.Comp1.Widgets.Count > module.Executor.Comp1.MaxWidgetCount + 1)
            return;

        module.Executor.Comp1.Widgets.Remove(widget.Id);

        module.Executor.Comp1.Widgets.Add(widget.Id, widget);
        module.PacketSystem.TryWrapSystemCall(() => module.PacketSystem.UserInterfaceSystem.ServerSendUiMessage(
            module.Executor.Owner,
            ExecutorUiKey.Key,
            new WidgetSendMessage(widget)));
    }

    public InitWidgetMethod(PacketModule? module) : base(module)
    {
        Id = "init_widget";
        Module = module;
        ModuleExec = Func;
    }
}
