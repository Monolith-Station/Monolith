using Robust.Shared.Serialization;

namespace Content.Shared._KS14.Packets.Widgets;

[Serializable, NetSerializable]
public sealed class Widget(string id) : WidgetElement(id)
{
    public WidgetElement[] Elements = [];
}

[Serializable] [NetSerializable]
public abstract class WidgetElement(string id)
{
    public string Id = id;

    public int[] Margin = [0, 0, 0, 0];
    public int[] Size = [150, 50];

    public string HorizontalAligment = "Stretch";
    public string VerticalAligment = "Stretch";
}

[Serializable, NetSerializable]
public sealed class WidgetButton(string id) : WidgetElement(id)
{
    public string Text = "Click me!";
    public string MethodName = "method";

    public WidgetButton(string id, string text, string methodName) : this(id)
    {
        Text = text;
        MethodName = methodName;
    }
}

[Serializable, NetSerializable]
public sealed class WidgetContainer(string id) : WidgetElement(id)
{
    public WidgetElement[] Elements = [];

    public WidgetContainer(string id, WidgetElement[] elements) : this(id)
    {
        Elements = elements;
    }
}

[Serializable] [NetSerializable]
public sealed class WidgetLabel(string id) : WidgetElement(id)
{
    public string Text = "Label";

    public WidgetLabel(string id, string text) : this(id)
    {
        Text = text;
    }
}

[Serializable] [NetSerializable]
public sealed class WidgetTextEdit(string id) : WidgetElement(id)
{
    public bool ReadOnly;

    public WidgetTextEdit(string id, bool readOnly) : this(id)
    {
        ReadOnly = readOnly;
    }
}
