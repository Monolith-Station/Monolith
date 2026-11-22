using Content.Shared._NF.Shipyard.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Shipyard;

public sealed class ShipyardShuttlePurchaseEvent(EntityUid shuttle, EntityUid purchaser, ProtoId<VesselPrototype> protoId)
{
    public EntityUid Shuttle { get;  } = shuttle;
    public EntityUid Purchaser { get; } = purchaser;
    public ProtoId<VesselPrototype> ProtoId { get; } = protoId;
}
