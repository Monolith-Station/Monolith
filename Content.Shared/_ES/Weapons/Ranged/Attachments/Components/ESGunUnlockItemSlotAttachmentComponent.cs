using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._ES.Weapons.Ranged.Attachments.Components;

// MONO COMPONENT - NOT ORIGINAL ES
/// <summary>
/// A <see cref="ESGunAttachmentComponent"/> that unlocks a specified item slot and relocks it when it's removed.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(ESSharedGunAttachmentsSystem))]
public sealed partial class ESGunUnlockItemSlotAttachmentComponent : Component
{
    [DataField("slots", required: true)]
    [AlwaysPushInheritance]
    public List<string> Slots;
}
