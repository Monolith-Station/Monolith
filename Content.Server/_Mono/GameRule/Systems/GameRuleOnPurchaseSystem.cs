using Content.Server.GameTicking;
using Content.Shared._Mono.Shipyard;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.GameRule.Systems;

public sealed partial class GameRuleOnPurchaseSystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipyardShuttlePurchaseEvent>(OnPurchase);
    }

    private void OnPurchase(ShipyardShuttlePurchaseEvent args)
    {
        if (!_proto.TryIndex(args.ProtoId, out var proto)
            || proto.GameRuleOnPurchase == null)
            return;

        _ticker.StartGameRule(proto.GameRuleOnPurchase);
    }
}