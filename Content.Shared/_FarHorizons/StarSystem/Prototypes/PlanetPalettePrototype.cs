using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Prototypes;

[Prototype]
public sealed partial class PlanetPalettePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public Color Color1 = default!;
    [DataField(required: true)] public Color Color2 = default!;
    [DataField(required: true)] public Color Color3 = default!;
    [DataField(required: true)] public Color Color4 = default!;
}