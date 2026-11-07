using Content.Shared.Actions;
using Content.Shared.Teleportation;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Teleportation;

[RegisterComponent]
public sealed partial class SeatEjectionComponent : Component
{
    [DataField]
    public EntityUid? ActionUid;

    [DataField]
    public EntProtoId ActionProto = "ActionSeatEject";

    [DataField]
    public TeleportSpecifier Specifier = new();

    [DataField]
    public bool Enabled = true;
}

public sealed partial class SeatEjectionEjectEvent : InstantActionEvent {}
