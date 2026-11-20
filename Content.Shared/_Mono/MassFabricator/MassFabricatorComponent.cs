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

    [AutoNetworkedField]
    public Dictionary<string, int> ReservedMaterials = new();

    [DataField]
    public Color EyeColor = Color.FromHex("#379DFF");

    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<MassFabricatorBlueprintPrototype>> AvailableBlueprints = new();

    [AutoNetworkedField]
    public HashSet<ProtoId<MassFabricatorBlueprintPrototype>> ChipBlueprints = new();

    public bool HasBlueprint(ProtoId<MassFabricatorBlueprintPrototype> id) =>
        AvailableBlueprints.Contains(id) || ChipBlueprints.Contains(id);

    [DataField, AutoNetworkedField]
    public ProtoId<MassFabricatorBlueprintPrototype>? SelectedBlueprint;

    [AutoNetworkedField]
    public string? SelectedTarget;
}
