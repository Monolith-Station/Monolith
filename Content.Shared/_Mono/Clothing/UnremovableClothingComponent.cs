using Content.Shared.Clothing.EntitySystems;
using Robust.Shared.GameStates;
using Content.Shared.Inventory;

namespace Content.Shared.Clothing.Components;

/// <summary>
/// The component prohibits the player from taking off clothes on them that have this component unless toggled by UnremoveableClothingRemoverComponent with a whitelist.
/// </summary>
[NetworkedComponent, AutoGenerateComponentState]
[RegisterComponent]
[Access(typeof(UnremovableClothingSystem))]
public sealed partial class UnremovableClothingComponent : Component, IClothingSlots
{
    /// <summary>
    /// Toggles the unremoveability of clothing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsUnremovable = true;

    /// <summary>
    /// Used for TryGetInventoryEntity, checks for these slots when a UnremoveableClothingRemoverComponent is applied to an entity with an inventory.
    /// </summary>
    [DataField]
    public SlotFlags Slots { get; set; } = SlotFlags.All;
}
