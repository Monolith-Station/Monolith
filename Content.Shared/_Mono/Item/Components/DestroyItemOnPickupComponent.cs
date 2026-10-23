namespace Content.Shared._Mono.Item.Components;

[RegisterComponent]
public sealed partial class DestroyItemOnPickupComponent : Component
{
    [DataField]
    public LocId Popup = "chimera-organ-disintegrate";
}