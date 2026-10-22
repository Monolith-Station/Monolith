using Content.Shared._FarHorizons.StarSystem.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Helpers;

[DataDefinition]
public sealed partial class PlanetaryLiquid
{
    [ViewVariables(VVAccess.ReadWrite)] public Color Color;
    [ViewVariables(VVAccess.ReadWrite)] public Color ShallowColor;
    [ViewVariables(VVAccess.ReadWrite)] public float Level;
    [ViewVariables(VVAccess.ReadWrite)] public float RiverFrequency;
    [ViewVariables(VVAccess.ReadWrite)] public float RiverThreshold;
    [ViewVariables(VVAccess.ReadWrite)] public float Specularity;
    [ViewVariables(VVAccess.ReadWrite)] public bool Emmissive;
    [ViewVariables(VVAccess.ReadWrite)] public float Emission;

    public PlanetaryLiquid(PlanetaryLiquidTypePrototype proto)
    {
        Color = proto.Color;
        ShallowColor = proto.ShallowColor;
        Level = proto.Level;
        RiverFrequency = proto.RiverFrequency;
        RiverThreshold = proto.RiverThreshold;
        Specularity = proto.Specularity;
        Emmissive = proto.Emissive;
        Emission = proto.Emission;
    }
}
