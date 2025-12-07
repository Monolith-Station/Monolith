namespace Content.Goobstation.Server.Weapons.BatterySlotRequiresItemToggle;

[RegisterComponent]
public sealed partial class BatterySlotRequiresToggleComponent : Component
{
    [DataField(required: true)]
    public string ItemSlot = string.Empty;

    [DataField]
    public bool Inverted;
}
