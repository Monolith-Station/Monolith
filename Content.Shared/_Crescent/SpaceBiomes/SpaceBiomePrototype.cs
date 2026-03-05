using Robust.Shared.Prototypes;

namespace Content.Shared._Crescent.SpaceBiomes;

[Prototype("ambientSpaceBiome")]
public sealed class SpaceBiomePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = "";

    [DataField(required: false)]
    public string Description = "";
}
