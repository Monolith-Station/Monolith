using Content.Shared._ES.Weapons.Ranged.Attachments;
using Content.Shared.Wieldable;
using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Ranged.Components;

/// <summary>
/// Applies an accuracy bonus upon wielding.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedWieldableSystem), typeof(ESSharedGunAttachmentsSystem))] // Mono - extend access
public sealed partial class GunWieldBonusComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("minAngle"), AutoNetworkedField]
    public Angle MinAngle = Angle.FromDegrees(-43);

    /// <summary>
    /// Mono - used for attachments to allow changing without cucking the comp ughughughu
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Angle MinAngleModified;

    /// <summary>
    /// Angle bonus applied upon being wielded.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("maxAngle"), AutoNetworkedField]
    public Angle MaxAngle = Angle.FromDegrees(-43);

    /// <summary>
    /// Mono - used for attachments to allow changing without cucking the comp ughughughu
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Angle MaxAngleModified;

    /// <summary>
    /// Recoil bonuses applied upon being wielded.
    /// Higher angle decay bonus, quicker recovery.
    /// Lower angle increase bonus (negative numbers), slower buildup.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle AngleDecay = Angle.FromDegrees(0);

    /// <summary>
    /// Mono - used for attachments to allow changing without cucking the comp ughughughu
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Angle AngleDecayModified;

	/// <summary>
    /// Recoil bonuses applied upon being wielded.
    /// Higher angle decay bonus, quicker recovery.
    /// Lower angle increase bonus (negative numbers), slower buildup.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle AngleIncrease = Angle.FromDegrees(0);

    /// <summary>
    /// Mono - used for attachments to allow changing without cucking the comp ughughughu
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Angle AngleIncreaseModified;

    [DataField]
    public LocId? WieldBonusExamineMessage = "gunwieldbonus-component-examine";
}
