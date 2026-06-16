namespace Content.Server._Mono.Drill;

/// <summary>
/// Drill that destroys tiles. Behavior is based on DrillType.
/// </summary>
[RegisterComponent]
public sealed partial class ShipDrillComponent : Component
{
    [DataField]
    public float DrillOffsetX = 0;

    [DataField]
    public float DrillOffsetY = 1;

    [DataField]
    public float DrillLength = 3;

    [DataField]
    public float DrillWidth = 3;

    [DataField]
    public string[] TileWhitelist =
    [
        "FloorCaveDrought", "FloorAsteroidSand", "FloorIce",
        "FloorBasalt", "FloorChromite", "FloorLowDesert",
        "Lattice",
    ];

    [DataField]
    public DrillType? DrillType = default!;
}
