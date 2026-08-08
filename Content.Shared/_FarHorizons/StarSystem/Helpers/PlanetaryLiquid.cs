using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Shared.Prototypes;

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

    public PlanetaryLiquid(System.Random rand, IPrototypeManager protoMan, ProtoId<PlanetaryLiquidTypePrototype> protoId)
    {
        var proto = protoMan.Index(protoId);
        Color = proto.Color;
        ShallowColor = proto.ShallowColor;
        Level = proto.Level.RollValue(rand);
        RiverFrequency = proto.RiverFrequency.RollValue(rand);
        RiverThreshold = proto.RiverThreshold.RollValue(rand);
        Specularity = proto.Specularity.RollValue(rand);
        Emmissive = proto.Emissive;
        Emission = proto.Emission.RollValue(rand);
    }
}