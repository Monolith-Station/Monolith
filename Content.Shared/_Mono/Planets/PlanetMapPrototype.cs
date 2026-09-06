using Content.Shared.Parallax.Biomes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Mono.Planets;

[Prototype("planetMap")]
public sealed partial class PlanetMapPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public string Name = default!;
    // depth of the bottom layer; layers are listed top-down.
    [DataField] public int BaseDepth;
    [DataField] public ComponentRegistry Components = new();
    [DataField(required: true)] public List<PlanetMapLayer> Layers = new();
}

[ImplicitDataDefinitionForInheritors]
public abstract partial class PlanetMapLayer
{
    [DataField] public ComponentRegistry Components = new();
}

public sealed partial class PlanetFileLayer : PlanetMapLayer
{
    [DataField(required: true)] public ResPath Map;
}

public sealed partial class PlanetBiomeLayer : PlanetMapLayer
{
    [DataField(required: true)] public ProtoId<BiomeTemplatePrototype> Biome;
    [DataField] public int SeedOffset;
}
