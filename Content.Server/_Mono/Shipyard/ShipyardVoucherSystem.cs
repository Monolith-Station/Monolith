using Content.Server._NF.Shipyard.Components;
using Content.Shared.Examine;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Shipyard;

public sealed partial class ShipyardVoucherSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipyardVoucherComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<ShipyardVoucherComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var remainingTime = ent.Comp.NextBuyAt - _timing.CurTime;

        if (ent.Comp.DestroyOnEmpty == true)
            args.PushMarkup(Loc.GetString("voucher-current-redemptions", ("count", ent.Comp.RedemptionsLeft)));
        else
            args.PushMarkup(Loc.GetString("voucher-infinite-redemptions"));

        if (remainingTime >= TimeSpan.FromSeconds(60))
            args.PushMarkup(Loc.GetString("voucher-current-cooldown-minutes", ("cooldown", remainingTime.TotalMinutes)));
        else if (remainingTime >= TimeSpan.FromSeconds(0))
            args.PushMarkup(Loc.GetString("voucher-current-cooldown-seconds", ("cooldown", remainingTime.TotalSeconds)));
    }
}