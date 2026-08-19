using Content.Shared.Actions;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared._Forge.ShipyardService;

[Serializable, NetSerializable]
public enum ShipyardServiceUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum ShipyardServiceAction : byte
{
    Repair,
    UpgradeParts,
    Reinforce,
    Plastitanium
}

[Serializable, NetSerializable]
public sealed class ShipyardServiceBoundUserInterfaceState : BoundUserInterfaceState
{
    public int Balance;
    public NetEntity? SelectedShuttle;
    public List<ShipyardServiceShuttleEntry> Shuttles = new();
    public ShipyardServiceQuote Quote = new();
    public List<ShipyardServiceUpgradeMarker> Markers = new();
}

[Serializable, NetSerializable]
public sealed class ShipyardServiceShuttleEntry
{
    public NetEntity Shuttle;
    public string Name = string.Empty;
    public string ClassLabel = string.Empty;
}

[Serializable, NetSerializable]
public sealed class ShipyardServiceQuote
{
    public bool HasShuttle;
    public string ShuttleName = string.Empty;
    public string ClassLabel = string.Empty;
    public int VesselPrice;
    public int OccupancyFee;
    public bool OccupancyDue;

    public int RepairCount;
    public int RepairWorkCost;
    public int RepairCost;
    public bool RepairOnCooldown;
    public TimeSpan RepairReadyAt;

    public int PartCount;
    public int PartCost;

    public int ReinforceCount;
    public int ReinforceCost;

    public int PlastitaniumCount;
    public int PlastitaniumCost;
}

[Serializable, NetSerializable]
public sealed class ShipyardServiceUpgradeMarker
{
    public int Id;
    public NetEntity Entity;
    public Vector2 LocalPosition;
    public Vector2i Tile;
    public ShipyardServiceAction Action;
    public int Cost;
    public int Count;
    public bool IsTile;
}

[Serializable, NetSerializable]
public sealed class ShipyardServiceSelectMessage : BoundUserInterfaceMessage
{
    public NetEntity Shuttle;

    public ShipyardServiceSelectMessage(NetEntity shuttle)
    {
        Shuttle = shuttle;
    }
}

[Serializable, NetSerializable]
public sealed class ShipyardServicePurchaseMessage : BoundUserInterfaceMessage
{
    public ShipyardServiceAction Action;

    public ShipyardServicePurchaseMessage(ShipyardServiceAction action)
    {
        Action = action;
    }
}

[Serializable, NetSerializable]
public sealed class ShipyardServiceUpgradeMarkedMessage : BoundUserInterfaceMessage
{
    public List<ShipyardServiceMarkerKey> Targets = new();

    public ShipyardServiceUpgradeMarkedMessage(List<ShipyardServiceMarkerKey> targets)
    {
        Targets = targets;
    }
}

[Serializable, NetSerializable]
public sealed class ShipyardServiceMarkerKey
{
    public NetEntity Entity;
    public Vector2i Tile;
    public ShipyardServiceAction Action;
    public bool IsTile;
}

/// <summary>
/// Targeting action used to upgrade a single wall, window, or machine on a docked shuttle.
/// </summary>
public sealed partial class ShipyardServiceUpgradeTargetEvent : EntityWorldTargetActionEvent;
