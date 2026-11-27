using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.MassFabricator;

[RegisterComponent]
public sealed partial class MassFabricatorBlueprintChipComponent : Component
{
    [DataField]
    public HashSet<ProtoId<MassFabricatorBlueprintPrototype>> Blueprints = new();
}
