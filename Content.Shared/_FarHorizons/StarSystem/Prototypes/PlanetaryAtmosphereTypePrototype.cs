using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Prototypes;

[Prototype]
public sealed partial class PlanetaryAtmosphereTypePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public List<Color> Colors = default!;
    [DataField(required: true)] public FloatRangeValue Thickness = default!;
    [DataField(required: true)] public FloatRangeValue Density = default!;
    [DataField(required: true)] public List<Color> CloudColors = default!;
    [DataField(required: true)] public FloatRangeValue CloudCoverage = default!;
    [DataField(required: true)] public FloatRangeValue CloudScale = default!;
    [DataField(required: true)] public FloatRangeValue CloudDensity = default!;
}