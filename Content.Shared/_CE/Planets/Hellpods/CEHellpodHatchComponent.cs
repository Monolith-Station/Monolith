using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Alert;
using Content.Shared.DoAfter;

namespace Content.Shared._CE.Planets.Hellpods;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEHellpodHatchComponent : Component
{
    [DataField]
    public EntProtoId Pod = "CEHellpod";

    [DataField]
    public TimeSpan LaunchDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan EntryDelay = TimeSpan.FromSeconds(2);

    public TimeSpan? LaunchAt;
}

[Serializable, NetSerializable]
public sealed partial class CEHellpodBoardDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class CEHellpodExitAlertEvent : BaseAlertEvent;

[Serializable, NetSerializable]
public enum CEHellpodVisuals : byte
{
    Occupied,
}
