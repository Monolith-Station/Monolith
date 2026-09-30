using Content.Shared._FarHorizons.StarSystem.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Helpers;

[DataDefinition]
public sealed partial class PlanetaryAtmosphere
{
    [ViewVariables(VVAccess.ReadWrite)] public Color Color;
    [ViewVariables(VVAccess.ReadWrite)] public float Thickness;
    [ViewVariables(VVAccess.ReadWrite)] public float Density;
    [ViewVariables(VVAccess.ReadWrite)] public Color CloudColor;
    [ViewVariables(VVAccess.ReadWrite)] public float CloudCoverage;
    [ViewVariables(VVAccess.ReadWrite)] public float CloudScale;
    [ViewVariables(VVAccess.ReadWrite)] public float CloudDensity;

    public PlanetaryAtmosphere(PlanetaryAtmosphereTypePrototype proto)
    {
        Color = proto.Color;
        Thickness = proto.Thickness;
        Density = proto.Density;
        CloudColor = proto.CloudColor;
        CloudCoverage = proto.CloudCoverage;
        CloudScale = proto.CloudScale;
        CloudDensity = proto.CloudDensity;
    }
}
