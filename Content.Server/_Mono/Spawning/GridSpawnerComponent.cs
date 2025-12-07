using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Mono.Spawning;

/// <summary>
/// Immediately loads in a grid at the location of this entity.
/// </summary>
[RegisterComponent]
public sealed partial class GridSpawnerComponent : Component
{
    [DataField(required: true)]
    public ResPath Path = new("Maps/_Mono/Shuttles/World/wedge.yml");

    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? NameDataset = null;

    [DataField]
    public ComponentRegistry AddComponents = new();

    [DataField]
    public bool NameGrid = true;
}


