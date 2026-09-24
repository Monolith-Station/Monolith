using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Prototypes;

[Prototype]
public sealed partial class AsteroidBeltTypePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public string Shader = default!;
    [DataField(required: true)] public ProtoId<PlanetPalettePrototype> Palette;
}
