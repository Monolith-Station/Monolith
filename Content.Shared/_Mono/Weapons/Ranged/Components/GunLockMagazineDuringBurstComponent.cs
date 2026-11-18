namespace Content.Shared._Mono.Weapons.Ranged.Components;

/// <summary>
/// Locks this gun's magazine slot if it is actively firing a burst. Note that LockedFailPopup must be defined for the slot in YAML and not in this component.
/// </summary>
[RegisterComponent]
public sealed partial class GunLockMagazineDuringBurstComponent : Component
{
    [DataField]
    public List<string> SlotIds = new() { "gun_magazine", "gun_chamber" };
}