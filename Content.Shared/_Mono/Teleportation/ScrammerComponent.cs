using Content.Shared.Actions;
using Content.Shared.Teleportation;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Teleportation;

/// <summary>
/// Grants an action to randomly teleport.
/// </summary>
[RegisterComponent]
public sealed partial class ScrammerComponent : Component
{
    [DataField]
    public TeleportSpecifier Specifier;

    [DataField]
    public EntityUid? ActionUid = null;

    [DataField]
    public EntProtoId ActionProto = "ActionScrammerScram";

    [DataField]
    public bool Enabled = true;

    /// <summary>
    /// Whether to respect ItemToggle toggling. If this is on a borg, will make the action disable on EMP.
    /// </summary>
    [DataField]
    public bool ItemToggleToggle = false;
}

public sealed partial class ScrammerScramEvent : InstantActionEvent { }
