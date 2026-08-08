using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Prototypes;

[Prototype]
public sealed partial class PlanetaryLiquidTypePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public Color Color = default!;
    [DataField(required: true)] public Color ShallowColor = default!;
    [DataField(required: true)] public FloatRangeValue Level = default!;
    [DataField(required: true)] public FloatRangeValue RiverFrequency = default!;
    [DataField(required: true)] public FloatRangeValue RiverThreshold = default!;
    [DataField(required: true)] public FloatRangeValue Specularity = default!;
    [DataField(required: true)] public bool Emissive = default!;
    [DataField(required: true)] public FloatRangeValue Emission = default!;
}