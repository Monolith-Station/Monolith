using Content.Shared.Parallax.Biomes.Layers;
using Content.Shared.Maps;
using Content.Shared.Procedural;
using Robust.Shared.Prototypes;

namespace Content.Shared.Parallax.Biomes;

/// <summary>
/// A preset group of biome layers to be used for a <see cref="BiomeComponent"/>
/// </summary>
[Prototype]
public sealed partial class BiomeTemplatePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField("layers")]
    public List<IBiomeLayer> Layers = new();

    [DataField]
    public List<ProtoId<DungeonRoomPrototype>> PointOfInterestRooms = new();

    [DataField]
    public List<ProtoId<ContentTileDefinition>> PointOfInterestTiles = new();

    [DataField]
    public int PointOfInterestSpacing = 256;
}
