using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono.Ships.Components;
using Content.Shared._Mono.Shipyard;
using Content.Shared._NF.Shipyard;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Ships.Systems;

/// <summary>
/// This handles shuttles with a limit.
/// </summary>
public sealed class LimitedShuttleSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly ShuttleDeedSystem _shuttleDeed = default!;

    private TimeSpan _lastUpdate = TimeSpan.Zero;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    private const double PoweredInactivityThreshold = 0.5;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AttemptShipyardShuttlePurchaseEvent>(OnAttemptShuttlePurchase);
        SubscribeLocalEvent<VesselComponent, ShipyardShuttlePurchaseEvent>(OnShuttlePurchase);
    }

    private void OnShuttlePurchase(Entity<VesselComponent> ent, ref ShipyardShuttlePurchaseEvent args)
    {
        EnsureComp<ShipActivityComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<VesselComponent>();

        if (_lastUpdate + _interval > _gameTiming.CurTime)
            return;

        _lastUpdate = _gameTiming.CurTime;

        while (query.MoveNext(out var uid, out _))
        {
            var inactivity = EnsureComp<ShipActivityComponent>(uid);

            if (inactivity.LastChecked + inactivity.CheckInterval > _gameTiming.CurTime)
                continue;

            inactivity.LastChecked = _gameTiming.CurTime;

            var isActive = IsActive(uid);

            if (isActive && inactivity.TimesInactive > 0)
                inactivity.TimesInactive = 0;

            if (!isActive)
                inactivity.TimesInactive++;

            inactivity.InactiveLastCheck = !isActive;

            if (!isActive && inactivity.GetMinutesInactive() >= inactivity.InactiveThresholdMinutes)
                inactivity.InactivePastThreshold = true;

            Dirty(uid, inactivity);
        }
    }

    private void OnAttemptShuttlePurchase(ref AttemptShipyardShuttlePurchaseEvent ev)
    {
        var query = EntityQueryEnumerator<VesselComponent>();
        var shuttleCount = 0;

        if (ev.Vessel.LimitActive <= 0)
            return;

        while (query.MoveNext(out var uid, out var targetVessel))
        {
            if (targetVessel.VesselId != ev.Vessel.ID)
                continue;

            // InactiveShipComponent isn't like a tag, it's more like ApcPowerReceiver. You need to check if it's inactive.
            if (!TryComp<ShipActivityComponent>(uid, out var inactivity) || inactivity.InactivePastThreshold)
                continue;

            shuttleCount++;
        }

        if (shuttleCount >= ev.Vessel.LimitActive)
        {
            ev.CancelReason = "shipyard-console-limited";
            ev.Cancel();
        }
    }

    private bool IsActive(Entity<VesselComponent?> vessel)
    {
        var consoles = new HashSet<Entity<ShuttleConsoleComponent>>();
        _lookup.GetGridEntities(vessel.Owner, consoles);

        var totalPowerEntities = 0;
        var powered = 0;

        // If the deed has no owner or the ship has no consoles, it's inactive.
        if (!_shuttleDeed.HasOwner(vessel.Owner)
            || consoles.Count == 0)
            return false;

        foreach (var ent in consoles)
        {
            if (!TryComp<ApcPowerReceiverComponent>(ent, out var powerReceiver))
                continue;

            if (powerReceiver.Powered) // should be powered even if not switched on.
                powered++;

            totalPowerEntities++;
        }

        var percentage = totalPowerEntities != 0 && powered != 0 ? powered / totalPowerEntities : 0;

        if (percentage >= PoweredInactivityThreshold)
            return true;

        return false;
    }
}
