using System.Numerics;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Helpers;

[DataDefinition]
public sealed partial class Star
{
    [ViewVariables] public float SolarMass;
    [ViewVariables] public float Luminocity;
    [ViewVariables] public float Radius;
    [ViewVariables] public float Temperature;
    [ViewVariables] public string Shader;
    [ViewVariables(VVAccess.ReadWrite)] public Color Color;
    [ViewVariables] public Vector2 Position;
    [ViewVariables] public string Name;
    [ViewVariables] public float Rotation;
    [ViewVariables] public PlanetaryRings? Rings;
    public const float NAV_PIXEL_SIZE = 500;
    public const float MAP_PIXEL_SIZE = 500;
    public const string STAR_ENTITY = "StarEntity";

    public Star(StarTypePrototype proto, IPrototypeManager protoMan)
    {
        SolarMass = Math.Clamp(proto.SolarMass, 0.08f, 8.0f); // Main Sequence stars

        Luminocity = SolarMass < 0.43f
            ? 0.23f * MathF.Pow(SolarMass, 2.3f)
            : SolarMass < 2.0f ? MathF.Pow(SolarMass, 4.0f) : 1.4f * MathF.Pow(SolarMass, 3.5f);

        Radius = SolarMass < 1.66f ? MathF.Pow(SolarMass, 0.9f) : 1.15f * MathF.Pow(SolarMass, 0.6f);

        Temperature = MathF.Pow(Luminocity / MathF.Pow(Radius, 2f), 0.25f) * 5778f;

        Name = proto.Name;
        Color = proto.Color;
        Shader = proto.Shader;
        Rotation = proto.Rotation;
        Position = Vector2.Zero;

        if (proto.Rings is { } rings)
            Rings = new PlanetaryRings(protoMan, protoMan.Index(rings));
    }
}
