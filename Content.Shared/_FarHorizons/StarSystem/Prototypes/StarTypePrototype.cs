using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Prototypes;

[Prototype]
public sealed partial class StarTypePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public string Shader = default!;
    [DataField(required: true)] public FloatRangeValue SolarMass = default!;
    [DataField(required: true)] public Color Color = default!;
    [DataField(required: true)] public List<StarOrbitSlot> Orbits = default!;
    [DataField(required: true)] public List<ProtoId<AsteroidBeltTypePrototype>> AsteroidBelts = default!;
}

[DataDefinition]
public sealed partial class StarOrbitSlot
{
    [DataField(required: true)] public OrbitType Type;
    [DataField] public float Prob = 1f;
}