using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.MassFabricator;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MassFabricatorComponent : Component
{
    [DataField, AutoNetworkedField]
    public int MaxBlueprintLoad = 100;

    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public int BlueprintLoad;

    [AutoNetworkedField]
    public List<EntityUid> Plans = new();

    [DataField, AutoNetworkedField]
    public int MaxCharge = 100;

    [DataField, AutoNetworkedField]
    public int RemainingCharge = 100;

    [DataField]
    public Color EyeColor = Color.FromHex("#379DFF");

    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<MassFabricatorBlueprintPrototype>> AvailableBlueprints = new();

    [DataField, AutoNetworkedField]
    public ProtoId<MassFabricatorBlueprintPrototype>? SelectedBlueprint;

    [AutoNetworkedField]
    public string? SelectedTarget;
}
