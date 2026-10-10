using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Prototypes;

[Prototype]
public sealed partial class StarSystemPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public ProtoId<StarTypePrototype> Star;
    [DataField] public List<StarSystemPlanet> Planets = new();
    [DataField] public StarSystemAsteroidBelt? AsteroidBelt;
}

[DataDefinition]
public sealed partial class StarSystemPlanet
{
    [DataField(required: true)] public ProtoId<PlanetTypePrototype> Planet;
    [DataField(required: true)] public float Distance;
    [DataField] public float Angle;
}

[DataDefinition]
public sealed partial class StarSystemAsteroidBelt
{
    [DataField(required: true)] public ProtoId<AsteroidBeltTypePrototype> Type;
    [DataField(required: true)] public float RadiusInner;
    [DataField(required: true)] public float RadiusOuter;
}
