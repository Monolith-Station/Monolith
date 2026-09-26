using Robust.Shared.GameStates;

namespace Content.Shared._Mono.MAWC.Shields;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShieldLinkReceiverComponent : Component
{
    [DataField(required: true)]
    public string LinkId = string.Empty;

    [ViewVariables, AutoNetworkedField]
    public readonly HashSet<EntityUid> LinkedSources = [];
}
