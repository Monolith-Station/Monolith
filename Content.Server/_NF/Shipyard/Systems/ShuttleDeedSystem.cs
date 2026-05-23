using Content.Server._NF.Shipyard.Systems;
using Content.Server._NF.Shipyard.Components;
using Content.Server._NF.Shipyard;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._Mono.Ships.Components;
using Content.Shared.Examine;
using Robust.Shared.Timing;

namespace Content.Server._NF.Shipyard;

public sealed partial class ShuttleDeedSystem : EntitySystem
{

    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShuttleDeedComponent, ExaminedEvent>(OnExamined);
    }

    public bool HasOwner(Entity<VesselComponent?> vessel)
    {
        return !TryComp<ShuttleDeedComponent>(vessel, out var deed) || deed.DeedHolder == null;
    }

    private void OnExamined(Entity<ShuttleDeedComponent> ent, ref ExaminedEvent args)
    {
        var comp = ent.Comp;
		TryComp<ShipyardVoucherComponent>(ent, out var voucher);

		if (!args.IsInDetailsRange)
            return;

        if (!string.IsNullOrEmpty(comp.ShuttleName))
        {
            var fullName = ShipyardSystem.GetFullName(comp);
            args.PushMarkup(Loc.GetString("shuttle-deed-examine-text", ("shipname", fullName)));
        }

        if (voucher != null)
		{
    		var remainingTime = voucher.NextBuyAt - _timing.CurTime;

			if (voucher.DestroyOnEmpty == true)
            	args.PushMarkup(Loc.GetString("voucher-current-redemptions", ("count", voucher.RedemptionsLeft)));
			else
            	args.PushMarkup(Loc.GetString("voucher-infinite-redemptions"));

			if (remainingTime >= TimeSpan.FromSeconds(60))
            	args.PushMarkup(Loc.GetString("voucher-current-cooldown-minutes", ("cooldown", remainingTime.TotalMinutes)));
        	else if (remainingTime >= TimeSpan.FromSeconds(0))
        		args.PushMarkup(Loc.GetString("voucher-current-cooldown-seconds", ("cooldown", remainingTime.TotalSeconds)));

		}
    }
}
