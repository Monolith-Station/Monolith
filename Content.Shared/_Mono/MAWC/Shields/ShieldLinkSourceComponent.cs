using Robust.Shared.GameStates;

namespace Content.Shared._Mono.MAWC.Shields;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShieldLinkSourceComponent : Component
{
    [DataField(required: true)]
    public string LinkId = string.Empty;

    [DataField]
    public float MaxRange = 12f;

    [DataField]
    public int MaxLinks = 4;

    [DataField]
    public float ShieldBonusPerLink = 100f;

    [ViewVariables, AutoNetworkedField]
    public readonly HashSet<EntityUid> LinkedShields = [];
}
