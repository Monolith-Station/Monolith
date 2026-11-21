using System.Threading.Tasks;
using Content.Shared._KS14.Packets.BUI;
using Content.Shared._KS14.Packets.Widgets;

namespace Content.Server._KS14.Packet.Modules.Base;

[ModuleMethod("BasePacketModule")]
public sealed class GetWidgetElementMethod : ModuleMethod
{
    public override PacketModule? Module { get; set; }

    public override Func<string, WidgetElement?> ModuleExec { get; }

    private WidgetElement? Func(string widgetElementId)
    {
        if (Module is not {  } module ||
            !module.PacketSystem.TryFindWidgetElement<WidgetElement>((module.Executor.Owner, module.Executor.Comp1), widgetElementId, out var element))
            return null;

        return element;
    }

    public GetWidgetElementMethod(PacketModule? module) : base(module)
    {
        Id = "get_element";
        Module = module;
        ModuleExec = Func;
    }
}
