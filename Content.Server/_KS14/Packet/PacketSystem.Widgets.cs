using System.Diagnostics.CodeAnalysis;
using Content.Server._KS14.Packet.Components;
using Content.Shared._KS14.Packets.Widgets;

namespace Content.Server._KS14.Packet;

public sealed partial class PacketSystem
{
    public bool TryFindWidgetElement<T>(Entity<PacketExecutorComponent> ent, string id, [NotNullWhen(returnValue: true)] out T? widgetElement) where T : WidgetElement
    {
        widgetElement = null;
        foreach (var widget in ent.Comp.Widgets.Values)
        {
            if (FindWidgetElement<T>(widget.Elements, id) is { } t)
            {
                widgetElement = t;
                return true;
            }
        }

        return false;
    }

    private T? FindWidgetElement<T>(WidgetElement[] widgetElements, string id) where T : WidgetElement
    {
        foreach (var widgetEl in widgetElements)
        {
            if (widgetEl is WidgetContainer widgetContainerEl)
            {
                if (FindWidgetElement<T>(widgetContainerEl.Elements, id) is { } el)
                    return el;
            }
            else
            {
                if (widgetEl.Id == id && widgetEl is T el)
                    return el;
            }
        }

        return null;
    }
}
