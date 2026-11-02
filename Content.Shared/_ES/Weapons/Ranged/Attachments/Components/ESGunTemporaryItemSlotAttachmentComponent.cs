using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._ES.Weapons.Ranged.Attachments.Components;

// MONO COMPONENT - NOT ORIGINAL ES
/// <summary>
/// A <see cref="ESGunAttachmentComponent"/> that adds an item slot (or multiple) and removes them when it is removed from the gun.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(ESSharedGunAttachmentsSystem))]
public sealed partial class ESGunTemporaryItemSlotAttachmentComponent : Component
{
    /// <summary>
    /// ID to use for the item slot.
    /// </summary>
    [DataField(required: true)]
    public string SlotName { get; set; } = "temporary_slot";

    /// <summary>
    /// Item slot to add temporarily.
    /// </summary>
    [DataField(required: true)]
    public ItemSlot ItemSlot = new();
}
