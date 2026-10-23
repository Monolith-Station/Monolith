namespace Content.Server._Mono.Fuel;

[RegisterComponent]
public sealed partial class MaterialFuelComponent : Component
{
    [DataField]
    public int ConsumptionRate = 1;

    [DataField]
    public int BaseMaterialComposition = 0;
}
