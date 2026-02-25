using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Claws.Components;

/// <summary>
/// This is claw component used for <see cref="SharedClawsSystem"/> System.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ClawsComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<ClawPrototype> ClawStage;

    [DataField, AutoNetworkedField]
    public Dictionary<int, ProtoId<ClawPrototype>> Claws;

    [DataField, AutoPausedField]
    public TimeSpan GrowTimer = TimeSpan.Zero;

    [AutoPausedField]
    public TimeSpan DeclawItemHoldTimer = TimeSpan.Zero;
}
