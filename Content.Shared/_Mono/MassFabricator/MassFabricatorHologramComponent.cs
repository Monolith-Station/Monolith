using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.MassFabricator;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class MassFabricatorHologramComponent : Component
{
    public const float FadeSeconds = 1.2f;

    [DataField, AutoNetworkedField]
    public ProtoId<MassFabricatorBlueprintPrototype> Blueprint = default!;

    [AutoNetworkedField]
    public EntityUid Fabricator;

    [DataField, AutoNetworkedField]
    public MassFabricatorBlueprintKind Kind;

    [DataField, AutoNetworkedField]
    public string Target = string.Empty;

    [AutoNetworkedField]
    public MassFabricatorPrintState PrintState;

    [AutoNetworkedField]
    public TimeSpan PrintStarted;

    [AutoNetworkedField]
    public float PrintDuration;

    [AutoNetworkedField]
    public Dictionary<string, int> ReservedMaterials = new();

    [AutoNetworkedField]
    public bool AutomaticPrint;

    [AutoNetworkedField]
    public bool LoadReleased;
}
