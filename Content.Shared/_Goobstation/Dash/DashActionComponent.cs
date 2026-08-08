using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Goobstation.Dash;

[RegisterComponent]
public sealed partial class DashActionComponent : Component
{
    [DataField]
    public string? ActionProto;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ActionUid;
}

public sealed partial class DashActionEvent : WorldTargetActionEvent
{
    [DataField]
    public float Distance = 4.65f;

    [DataField]
    public float Speed = 9.65f;

    /// <summary>
    /// Whether you need gravity to perform the dash. Keep in mind there's no friction without gravity so if this
    /// is false, the performer gets every chance to be launched straight to Ohio on dashing without gravity.
    /// </summary>
    [DataField]
    public bool NeedsGravity = true;

    /// <summary>
    /// Whether dash distance and speed are affected by performer's speed modifiers. Should be true most of the time.
    /// </summary>
    [DataField]
    public bool AffectedBySpeed = true;
}
