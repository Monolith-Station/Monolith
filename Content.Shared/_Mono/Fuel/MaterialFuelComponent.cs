using Content.Shared.Materials;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Fuel;

[RegisterComponent, NetworkedComponent]
public sealed partial class MaterialFuelComponent : Component
{
    /// <summary>
    /// If not specified - picks first in composition.
    /// </summary>
    [DataField]
    public ProtoId<MaterialPrototype>? Material;

    [DataField]
    public int ConsumptionRate = 1;

    [DataField]
    public int BaseMaterialComposition;
}
