using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Forge.Clothing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModsuitGauntletToolsComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId UrkProto = "ShipRepairDevice";

    [DataField, AutoNetworkedField]
    public EntProtoId OmnitoolProto = "OmnitoolModsuitGauntlet";

    [DataField, AutoNetworkedField]
    public EntProtoId WelderProto = "WelderExperimental";

    [DataField, AutoNetworkedField]
    public EntProtoId NaniteApplicatorProto = "NaniteApplicatorExperimental";

    /// <summary>
    /// Optional fifth integrated tool (e.g. Drake hardsuits — grappling gun).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId AuxiliaryProto = "WeaponGrapplingGun";

    /// <summary>
    /// Optional sixth integrated tool (e.g. Omnissia — RPD).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId PipingProto = "RPD";

    [DataField, AutoNetworkedField]
    public EntityUid? UrkEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? OmnitoolEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? WelderEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? NaniteApplicatorEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? AuxiliaryEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? PipingEntity;

    [DataField, AutoNetworkedField]
    public bool UrkInHand;

    [DataField, AutoNetworkedField]
    public bool OmnitoolInHand;

    [DataField, AutoNetworkedField]
    public bool WelderInHand;

    [DataField, AutoNetworkedField]
    public bool NaniteApplicatorInHand;

    [DataField, AutoNetworkedField]
    public bool AuxiliaryInHand;

    [DataField, AutoNetworkedField]
    public bool PipingInHand;

    /// <summary>
    /// Which integrated tool slots are available. Omnissia gauntlets use the default (all four).
    /// </summary>
    [DataField, AutoNetworkedField]
    public ModsuitGauntletEnabledSlots EnabledSlots = ModsuitGauntletEnabledSlots.All;

    /// <summary>
    /// Radial menu uses each tool prototype icon instead of fixed Omnissia artwork.
    /// </summary>
    [DataField]
    public bool UsePrototypeMenuIcons;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class ModsuitGauntletToolComponent : Component
{
    [DataField]
    public EntityUid Gauntlets;

    /// <summary>
    /// Tool is stowed in nullspace; client hides the sprite while this is true.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool StoredHidden;
}
