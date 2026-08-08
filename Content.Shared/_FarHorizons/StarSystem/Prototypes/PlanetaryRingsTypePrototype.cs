using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Prototypes;

[Prototype]
public sealed partial class PlanetaryRingsTypePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public FloatRangeValue RadiusInner = default!;
    [DataField(required: true)] public float WidthMin = default!;
    [DataField(required: true)] public float RadiusOuterMax = default!;
    [DataField(required: true)] public FloatRangeValue BandFrequency = default!;
    [DataField(required: true)] public List<ProtoId<PlanetPalettePrototype>> Palettes = default!;
}