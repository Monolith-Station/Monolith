using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Prototypes;

[Prototype]
public sealed partial class PlanetaryAtmosphereTypePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public Color Color = default!;
    [DataField(required: true)] public float Thickness;
    [DataField(required: true)] public float Density;
    [DataField(required: true)] public Color CloudColor = default!;
    [DataField(required: true)] public float CloudCoverage;
    [DataField(required: true)] public float CloudScale;
    [DataField(required: true)] public float CloudDensity;
}
