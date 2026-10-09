using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Prototypes;

[Prototype]
public sealed partial class PlanetaryLiquidTypePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public Color Color = default!;
    [DataField(required: true)] public Color ShallowColor = default!;
    [DataField(required: true)] public float Level;
    [DataField(required: true)] public float RiverFrequency;
    [DataField(required: true)] public float RiverThreshold;
    [DataField(required: true)] public float Specularity;
    [DataField(required: true)] public bool Emissive;
    [DataField(required: true)] public float Emission;
}
