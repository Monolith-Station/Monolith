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
    [DataField]
    public ResPath Path = "/Maps/_Mono/Shuttles/World/drone.yml";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? NameDataset = null;

    [DataField]
    public ComponentRegistry AddComponents = new();

    [DataField]
    public bool NameGrid = true;
}


