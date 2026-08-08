using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

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

    public PlanetaryAtmosphere(System.Random rand, IPrototypeManager protoMan, ProtoId<PlanetaryAtmosphereTypePrototype> protoId)
    {
        var proto = protoMan.Index(protoId);
        Color = rand.Pick(proto.Colors);
        Thickness = proto.Thickness.RollValue(rand);
        Density = proto.Density.RollValue(rand);
        CloudColor = rand.Pick(proto.CloudColors);
        CloudCoverage = proto.CloudCoverage.RollValue(rand);
        CloudScale = proto.CloudScale.RollValue(rand);
        CloudDensity = proto.CloudDensity.RollValue(rand);
    }
}