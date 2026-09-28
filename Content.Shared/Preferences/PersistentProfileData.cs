using Robust.Shared.Serialization;

namespace Content.Shared.Preferences;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial record PersistentProfileComponent
{
    [DataField]
    public string Data { get; set; } = string.Empty;

    [DataField]
    public bool Sticky { get; set; }

    public PersistentProfileComponent()
    {
    }

    public PersistentProfileComponent(string data, bool sticky)
    {
        Data = data;
        Sticky = sticky;
    }
}

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial record PersistentProfileItem
{
    [DataField]
    public string Data { get; set; } = string.Empty;

    [DataField]
    public bool Sticky { get; set; }

    public PersistentProfileItem()
    {
    }

    public PersistentProfileItem(string data, bool sticky)
    {
        Data = data;
        Sticky = sticky;
    }
}
