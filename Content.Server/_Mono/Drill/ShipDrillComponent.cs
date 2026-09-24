using System.Numerics;
using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Drill;

/// <summary>
/// Drill that destroys tiles. Behavior is based on DrillType.
/// </summary>
[RegisterComponent]
public sealed partial class ShipDrillComponent : Component
{
    [DataField]
    public Vector2 DrillOffsets = new(0, 1f);

    [DataField]
    public Vector2 DrillSize = new(2f, 2f);

    [DataField]
    public List<ProtoId<ContentTileDefinition>>? TileWhitelist =
    [
        "FloorCaveDrought", "FloorAsteroidSand", "FloorIce",
        "FloorBasalt", "FloorChromite", "FloorLowDesert",
        "Lattice",
    ];

    [DataField]
    public DrillType? DrillType = default!;
}
