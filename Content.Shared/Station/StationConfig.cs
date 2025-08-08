using Robust.Shared.Prototypes;

namespace Content.Shared.Station;

/// <summary>
/// A config for a station. Specifies name and component modifications.
/// </summary>
[DataDefinition]
public sealed partial class StationConfig
{
    [DataField("stationProto", required: true)]
    public EntProtoId StationPrototype;

    [DataField("components", required: true)]
    public ComponentRegistry StationComponentOverrides = default!;

    // Crescent - used to add components to grid. rn used for music & biome sys
    [DataField]
    public ComponentRegistry gridComponents = new();
    // Crescent
}

