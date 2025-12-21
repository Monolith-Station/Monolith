namespace Content.Shared._Mono.Shipyard;

public sealed class ShipyardShuttlePurchaseEvent(EntityUid shuttle, EntityUid purchaser)
{
    public EntityUid Shuttle { get;  } = shuttle;
    public EntityUid Purchaser { get; } = purchaser;
}
