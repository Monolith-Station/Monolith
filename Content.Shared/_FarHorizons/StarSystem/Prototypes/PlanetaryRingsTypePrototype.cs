using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Prototypes;

[Prototype]
public sealed partial class PlanetaryRingsTypePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public float RadiusInner; // multiples of the body's radius
    [DataField(required: true)] public float RadiusOuter;
    [DataField(required: true)] public float BandFrequency;
    [DataField(required: true)] public ProtoId<PlanetPalettePrototype> Palette;
}
