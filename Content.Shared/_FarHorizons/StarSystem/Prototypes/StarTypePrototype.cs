using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Prototypes;

[Prototype]
public sealed partial class StarTypePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public string Name = default!;
    [DataField(required: true)] public string Shader = default!;
    [DataField(required: true)] public float SolarMass;
    [DataField(required: true)] public Color Color = default!;
    [DataField] public float Rotation;
    [DataField] public ProtoId<PlanetaryRingsTypePrototype>? Rings;
}
