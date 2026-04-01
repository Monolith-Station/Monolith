using Content.Server.Cargo.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Cargo;

public sealed class DriftingPriceSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private float _updateSpacing = 1f;
    private float _updateAccum = 0f;

    public override void Initialize()
    {
        SubscribeLocalEvent<DriftingPriceComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<DriftingPriceComponent, PriceCalculationEvent>(OnGetPrice);
    }

    private void OnInit(Entity<DriftingPriceComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.CurrentPrice = _random.NextDouble(ent.Comp.MinInitial, ent.Comp.MaxInitial);
    }

    private void OnGetPrice(Entity<DriftingPriceComponent> ent, ref PriceCalculationEvent args)
    {
        args.Price += ent.Comp.CurrentPrice;
    }

    public override void Update(float frameTime)
    {
        _updateAccum += frameTime;
        if (_updateAccum < _updateSpacing)
            return;
        _updateAccum -= _updateSpacing;

        var query = EntityQueryEnumerator<DriftingPriceComponent>();
        while (query.MoveNext(out var uid, out var price))
        {
            var driftTotal = price.CurrentPrice * price.DriftRate * _updateSpacing;
            var priceScale = price.CurrentPrice / price.BasePrice;
            var priceOffset = Math.Pow(priceScale, price.Stability);
            var driftPoint = Math.Pow(_random.NextDouble(), priceOffset) * 2 - 1;
            price.CurrentPrice += driftTotal * driftPoint;
        }
    }
}
