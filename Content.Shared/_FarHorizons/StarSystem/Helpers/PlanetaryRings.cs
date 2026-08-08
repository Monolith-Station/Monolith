using System.Linq;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._FarHorizons.StarSystem.Helpers;

[DataDefinition]
public sealed partial class PlanetaryRings
{
    [ViewVariables(VVAccess.ReadWrite)] public float RadiusInner;
    [ViewVariables(VVAccess.ReadWrite)] public float RadiusOuter;
    [ViewVariables(VVAccess.ReadWrite)] public float BandFrequency;
    [ViewVariables(VVAccess.ReadWrite)] public Color Color1;
    [ViewVariables(VVAccess.ReadWrite)] public Color Color2;
    [ViewVariables(VVAccess.ReadWrite)] public Color Color3;

    public PlanetaryRings(System.Random rand, IPrototypeManager protoMan, ProtoId<PlanetaryRingsTypePrototype> protoId)
    {
        var proto = protoMan.Index(protoId);
        RadiusInner = proto.RadiusInner.RollValue(rand);
        RadiusOuter = rand.NextFloat(RadiusInner + proto.WidthMin, proto.RadiusOuterMax);
        BandFrequency = proto.BandFrequency.RollValue(rand);

        var palettes = proto.Palettes.Select(p => protoMan.Index(p)).ToList();
        var palette = rand.Pick(palettes);

        Color1 = palette.Color1;
        Color2 = palette.Color2;
        Color3 = palette.Color3;
    }
}