using System.Numerics;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

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
    [ViewVariables] public int BasePrettiness;

    public const float NAV_PIXEL_SIZE = 10;
    public const float MAP_PIXEL_SIZE = 10;
    public const string PLANET_ENTITY = "PlanetEntity";

    public Planet(Vector2 position,
                  string name,
                  float earthMass,
                  float rotation,
                  PlanetaryAtmosphere? atmosphere,
                  PlanetaryLiquid? liquid,
                  ProtoId<PlanetPalettePrototype> palette,
                  string shader,
                  float hueShift,
                  float saturationShift,
                  PlanetCustomValues customData,
                  PlanetaryRings? rings = null,
                  int basePrettiness = -100)
    {
        Position = position;
        Name = name;
        EarthMass = earthMass;
        Rotation = rotation;
        Radius = GetRadius(EarthMass);
        Atmosphere = atmosphere;
        Liquid = liquid;
        Palette = palette;
        Shader = shader;
        HueShift = hueShift;
        SaturationShift = saturationShift;
        CustomData = customData;
        Rings = rings;
        BasePrettiness = basePrettiness;
    }

    public static float GetRadius(float mass) => 
        mass switch
        {
            <= 2f => (float)Math.Pow(mass, 0.28f), // rocky planets
            <= 130f => 1.01f * (float)Math.Pow(mass, 0.59f), // neptune-likes
            _ => 12f * (float)Math.Pow(mass, -0.04f) // jupiter-likes
        };

    public int GetPettiness()
    {
        var prettiness = BasePrettiness;

        if (Rings != null)
            prettiness += 10;
        
        if (Atmosphere != null)
            prettiness += 5;
        
        if (Liquid != null)
            prettiness += 10;

        return prettiness;
    }

    public Vector2 GetPointOnOrbit(IRobustRandom rand, float spacing = 25f)
    {
        var angle = rand.Next() * 2f * MathF.PI;

        var radius = (Radius * NAV_PIXEL_SIZE) + spacing;

        var x = Position.X + (radius * MathF.Cos(angle));
        var y = Position.Y + (radius * MathF.Sin(angle));

        return new Vector2(x, y);
    }
}

[DataDefinition]
public sealed partial class PlanetCustomValues
{
    [ViewVariables(VVAccess.ReadWrite)] public Dictionary<string, float> Floats;
    [ViewVariables(VVAccess.ReadWrite)] public Dictionary<string, int> Ints;
    [ViewVariables(VVAccess.ReadWrite)] public Dictionary<string, Color> Colors;

    public PlanetCustomValues(System.Random rand, PlanetTypePrototype proto)
    {
        Floats = new Dictionary<string, float>();
        foreach (var (key, range) in proto.CustomData.Floats)
            Floats[key] = range.RollValue(rand);

        Ints = new Dictionary<string, int>();
        foreach (var (key, range) in proto.CustomData.Ints)
            Ints[key] = range.RollValue(rand);

        Colors = new Dictionary<string, Color>();
        foreach (var (key, range) in proto.CustomData.Colors)
            Colors[key] = range.RollValue(rand);
    }
}