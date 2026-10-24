using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Materials;

[RegisterComponent]
public sealed partial class ChangeBatteryOnMaterialComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, float> ChargingMaterials = [];
}
