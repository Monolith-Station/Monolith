using Robust.Shared.GameStates;

namespace Content.Shared._ES.Weapons.Ranged.Attachments.Components;

// MONO COMPONENT - NOT ORIGINAL ES
/// <summary>
/// A <see cref="ESGunAttachmentComponent"/> that changes a gun's spread & recoil control.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(ESSharedGunAttachmentsSystem))]
public sealed partial class ESGunRecoilAttachmentComponent : Component
{
    [DataField]
    public float RecoilRecoveryModifier = 1.0f;

    [DataField]
    public float RecoilIncreaseModifier = 1.0f;

    [DataField]
    public float MinSpreadModifier = 1.0f;

    [DataField]
    public float MaxSpreadModifier = 1.0f;

    [DataField]
    public float WieldRecoilRecoveryModifier = 1.0f;

    [DataField]
    public float WieldRecoilIncreaseModifier = 1.0f;

    [DataField]
    public float WieldMinSpreadModifier = 1.0f;

    [DataField]
    public float WieldMaxSpreadModifier = 1.0f;
}
