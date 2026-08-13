using System.Numerics;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Helpers;

[DataDefinition]
public sealed partial class Planet
{
    [ViewVariables] public Vector2 Position;
    [ViewVariables] public string Name;
    [ViewVariables] public float EarthMass;
    [ViewVariables] public float Radius;
    [ViewVariables] public float Rotation;
    [ViewVariables] public PlanetaryAtmosphere? Atmosphere;
    [ViewVariables] public PlanetaryLiquid? Liquid;
    [ViewVariables] public ProtoId<PlanetPalettePrototype> Palette;
    [ViewVariables] public string Shader;
    [ViewVariables] public float HueShift;
    [ViewVariables] public float SaturationShift;
    [ViewVariables] public PlanetCustomValues CustomData;
    [ViewVariables] public PlanetaryRings? Rings;

    public const float NAV_PIXEL_SIZE = 10;
    public const float MAP_PIXEL_SIZE = 10;
    public const string PLANET_ENTITY = "PlanetEntity";

    public Planet(PlanetTypePrototype proto, IPrototypeManager protoMan, Vector2 position)
    {
        Position = position;
        Name = proto.Name;
        Shader = proto.Shader;
        Palette = proto.Palette;
        Rotation = proto.Rotation;
        HueShift = proto.HueShift;
        SaturationShift = proto.SaturationShift;
        CustomData = proto.CustomData;

        EarthMass = proto.EarthMass;
        Radius = GetRadius(EarthMass);

        if (proto.Atmosphere is { } atmosphere)
            Atmosphere = new PlanetaryAtmosphere(protoMan.Index(atmosphere));

        if (proto.Liquid is { } liquid)
            Liquid = new PlanetaryLiquid(protoMan.Index(liquid));

        if (proto.Rings is { } rings)
            Rings = new PlanetaryRings(protoMan, protoMan.Index(rings));
    }

    public static float GetRadius(float mass) =>
        mass switch
        {
            <= 2f => (float)Math.Pow(mass, 0.28f), // rocky planets
            <= 130f => 1.01f * (float)Math.Pow(mass, 0.59f), // neptune-likes
            _ => 12f * (float)Math.Pow(mass, -0.04f) // jupiter-likes
        };
}

[DataDefinition]
public sealed partial class PlanetCustomValues
{
    [DataField] public Dictionary<string, float> Floats = new();
    [DataField] public Dictionary<string, int> Ints = new();
    [DataField] public Dictionary<string, Color> Colors = new();
}
