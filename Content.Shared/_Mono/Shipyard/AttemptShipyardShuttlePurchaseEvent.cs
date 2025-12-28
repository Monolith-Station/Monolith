using Content.Shared._NF.Shipyard.Prototypes;

namespace Content.Shared._Mono.Shipyard;

[ByRefEvent]
public sealed class AttemptShipyardShuttlePurchaseEvent(EntityUid shuttle, EntityUid purchaser, VesselPrototype vessel, LocId? cancelReason = null) : CancellableEntityEventArgs
{
    public EntityUid Shuttle { get;  } = shuttle;
    public EntityUid Purchaser { get; } = purchaser;
    public VesselPrototype Vessel { get; } = vessel;
    public LocId CancelReason { get; set;  } = cancelReason ?? "shipyard-console-denied";
}
