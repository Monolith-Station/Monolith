using Content.Shared.Inventory;

namespace Content.Shared._Mono.Weapons.Ranged.Components;

/// <summary>
/// Changes this gun's valid slots while it is currently firing a burst. Generally makes it unable to be worn as clothing (which would allow you to shoot 2 guns at once).
/// </summary>
[RegisterComponent]
public sealed partial class GunToggleClothingDuringBurstComponent : Component
{
    /// <summary>
    /// The slots to set it to when this gun is firing a burst.
    /// </summary>
    [DataField]
    public SlotFlags SlotsDuringBurst = SlotFlags.NONE;

    /// <summary>
    /// The slots to set it to when this gun is not firing a burst.
    /// </summary>
    [DataField]
    public SlotFlags IdleSlots = SlotFlags.SUITSTORAGE | SlotFlags.BACK;
}