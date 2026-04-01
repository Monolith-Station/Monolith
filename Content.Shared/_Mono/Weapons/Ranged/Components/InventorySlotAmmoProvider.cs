using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared._Mono.Weapons.Ranged.Components;

/// <summary>
/// Makes gun use ammo from the inventory slot entity
/// </summary>
[RegisterComponent, Virtual]
public partial class InventorySlotProviderComponent : AmmoProviderComponent
{
    [DataField]
    public string Slot = "back";
}
