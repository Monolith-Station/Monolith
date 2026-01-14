using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.DeviceLinking;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class RotationSignalControlComponent : Component
{
    [DataField]
    public ProtoId<SinkPortPrototype> TriggerPort = "Trigger";

    [DataField]
    public ProtoId<SinkPortPrototype> OnPort = "On";

    [DataField]
    public ProtoId<SinkPortPrototype> OffPort = "Off";
}
