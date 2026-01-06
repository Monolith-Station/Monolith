namespace Content.Server._Mono.ShipRepair;

/// <summary>
/// Raised to check if entity should be included in repair data.
/// </summary>
[ByRefEvent]
public record struct ShipRepairStoreQueryEvent(bool Repairable = true);

/// <summary>
/// Raised to check on the original of an entity we're trying to reinstate, if such an original still exists.
/// </summary>
[ByRefEvent]
public record struct ShipRepairReinstateQueryEvent(bool Repairable = true, bool Handled = false);
