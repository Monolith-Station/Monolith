using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Forge.ShipyardService.Components;

/// <summary>
/// Console on a shipyard that repairs and upgrades a currently docked shuttle.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShipyardServiceConsoleComponent : Component
{
    [DataField]
    public int RepairBaseCost = 0;

    [DataField]
    public int RepairPerObjectCost = 50;

    [DataField]
    public int PartUpgradeCost = 15000;

    [DataField]
    public int ReinforceCost = 10000;

    [DataField]
    public int PlastitaniumCost = 20000;

    [DataField]
    public TimeSpan RepairCooldown = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Machine part rating treated as "super" / T3 in this fork.
    /// </summary>
    [DataField]
    public int SuperPartRating = 4;

    [DataField]
    public Dictionary<string, EntProtoId> SuperPartPrototypes = new()
    {
        ["Capacitor"] = "SuperCapacitorStockPart",
        ["Manipulator"] = "PicoManipulatorStockPart",
        ["MatterBin"] = "SuperMatterBinStockPart",
        ["PowerCell"] = "PowerCellHyper"
    };

    /// <summary>
    /// Regular walls/windows upgraded to reinforced. Key is the current prototype id.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, EntProtoId> RegularUpgrades = new()
    {
        ["WallSolid"] = "WallReinforced",
        ["WallSolidDiagonal"] = "WallReinforcedDiagonal",
        ["WallShuttleInterior"] = "WallShuttle",
        ["WallShuttleDiagonal"] = "WallReinforcedDiagonal",
        ["Window"] = "ReinforcedWindow",
        ["WindowDiagonal"] = "ReinforcedWindowDiagonal",
        ["WindowDirectional"] = "WindowReinforcedDirectional"
    };

    /// <summary>
    /// Reinforced (or shuttle-grade) structures upgraded to plastitanium.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, EntProtoId> ReinforcedUpgrades = new()
    {
        ["WallReinforced"] = "WallPlastitanium",
        ["WallReinforcedDiagonal"] = "WallPlastitaniumDiagonal",
        ["WallShuttle"] = "WallPlastitanium",
        ["ShuttleWindow"] = "PlastitaniumWindow",
        ["ShuttleWindowDiagonal"] = "PlastitaniumWindowDiagonal",
        ["ReinforcedWindow"] = "PlastitaniumWindow",
        ["ReinforcedWindowDiagonal"] = "PlastitaniumWindowDiagonal",
        ["WindowReinforcedDirectional"] = "PlastitaniumWindow"
    };

    [ViewVariables]
    public EntityUid? SelectedShuttle;

    [DataField]
    public EntProtoId UpgradeAction = "ActionShipyardServiceUpgrade";
}
