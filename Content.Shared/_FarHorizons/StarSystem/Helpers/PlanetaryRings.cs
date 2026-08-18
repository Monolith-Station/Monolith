using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Shared.Prototypes;

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

    public PlanetaryRings(IPrototypeManager protoMan, PlanetaryRingsTypePrototype proto)
    {
        RadiusInner = proto.RadiusInner;
        RadiusOuter = proto.RadiusOuter;
        BandFrequency = proto.BandFrequency;

        var palette = protoMan.Index(proto.Palette);
        Color1 = palette.Color1;
        Color2 = palette.Color2;
        Color3 = palette.Color3;
    }
}
