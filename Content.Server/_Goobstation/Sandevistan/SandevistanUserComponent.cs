using Content.Shared._Goobstation.Wizard.Projectiles;
using Content.Shared._Goobstation.Sandevistan;
using Content.Shared.Abilities;
using Content.Shared.Actions;
using Robust.Shared.Audio;

// Ideally speaking this should be on the heart itself... but this also works.
namespace Content.Server._Goobstation.Sandevistan;

[RegisterComponent]
public sealed partial class SandevistanUserComponent : Component
{
    /// <summary>
    /// Marker component indicating that the Sandevistan is currently active.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public ActiveSandevistanUserComponent? Active;

    /// <summary>
    /// Time when the currently active Sandevistan will automatically shut down.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan? DisableAt;

    /// <summary>
    /// Time when the Sandevistan will finish recharging and become usable again.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan? RechargeAt;

    /// <summary>
    /// How long the Sandevistan remains active after activation.
    /// Configured by the implant's YAML prototype.
    /// </summary>
    [DataField]
    public TimeSpan ActiveDuration = TimeSpan.FromSeconds(8);

    /// <summary>
    /// How long the Sandevistan must recharge after being disabled.
    /// Configured by the implant's YAML prototype.
    /// </summary>
    [DataField]
    public TimeSpan RechargeDuration = TimeSpan.FromSeconds(40);

    [DataField]
    public string ActionProto = "ActionToggleSandevistan";

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ActionUid;

    [DataField]
    public float MovementSpeedModifier = 2f;

    [DataField]
    public float AttackSpeedModifier = 2f;

    [DataField]
    public SoundSpecifier? StartSound = new SoundPathSpecifier("/Audio/_Goobstation/Misc/sande_start.ogg");

    [DataField]
    public SoundSpecifier? EndSound = new SoundPathSpecifier("/Audio/_Goobstation/Misc/sande_end.ogg");

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? RunningSound;

    [ViewVariables(VVAccess.ReadOnly)]
    public DogVisionComponent? Overlay;

    [ViewVariables(VVAccess.ReadOnly)]
    public TrailComponent? Trail;

    [ViewVariables(VVAccess.ReadWrite)]
    public int ColorAccumulator = 0;
}