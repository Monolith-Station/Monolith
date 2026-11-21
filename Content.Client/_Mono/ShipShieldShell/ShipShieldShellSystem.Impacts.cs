using System.Numerics;
using Content.Shared._Mono.Ships;
using Robust.Shared.Map;

namespace Content.Client._Mono.ShipShieldShell;

public sealed partial class ShipShieldShellSystem
{
    private readonly List<ImpactBurst> _impacts = new();
    private readonly Dictionary<EntityUid, uint> _lastImpactIds = new();
    private readonly List<PredictedContact> _predictedContacts = new();
    private readonly Dictionary<(EntityUid Shield, NetEntity Source), (TimeSpan Contact, TimeSpan Burst)> _contactTimes = new();
    private readonly Dictionary<EntityUid, (TimeSpan ObservedAt, TimeSpan StartedAt)> _shatters = new();

    internal IReadOnlyList<ImpactBurst> ImpactBursts => _impacts;

    private void OnSurfaceContact(Entity<ShipShieldProjectorComponent> shield, ref ShipShieldSurfaceContactEvent args)
    {
        if (!_timing.IsFirstTimePredicted || args.Source is not { } source)
            return;

        var now = _timing.CurTime;
        var key = (shield.Owner, GetNetEntity(source));
        if (args.BodyImpactSpeed == null && _contactTimes.TryGetValue(key, out var last) &&
            (args.Pushing ? now - last.Burst < _timing.TickPeriod * 3 : now - last.Contact <= ShipShieldProjectorComponent.PressureGrace))
        {
            _contactTimes[key] = (now, last.Burst);
            return;
        }

        _contactTimes[key] = (now, now);
        _predictedContacts.Add(new PredictedContact(shield.Owner, key.Item2, args.Point, args.Map, now));
        var damage = args.BodyImpactSpeed is { } speed ? _containment.GetBodyImpactDamage(shield.Comp, source, speed) : 0f;
        _impacts.Add(new ImpactBurst(shield.Owner, args.Point, args.Map, _timing.FrameStartTime, now,
            SharedShipShieldShellSystem.GetBodyImpactPower(damage)));
    }

    private bool ConfirmContact(EntityUid shield, ShipShieldImpact impact)
    {
        if (impact.Source is not { } source)
            return false;

        var best = -1;
        var tolerance = _timing.TickPeriod * 3;
        for (var i = 0; i < _predictedContacts.Count; i++)
        {
            var predicted = _predictedContacts[i];
            var delta = (predicted.At - impact.At).Duration();
            if (predicted.Shield != shield || predicted.Source != source || predicted.Map != impact.Map ||
                delta > tolerance || Vector3.DistanceSquared(predicted.Point, impact.Origin) > 0.09f)
                continue;

            best = i;
            tolerance = delta;
        }

        if (best < 0)
            return false;

        _predictedContacts.RemoveAt(best);
        return true;
    }

    public override void FrameUpdate(float frameTime)
    {
        var now = _timing.CurTime;
        var frame = _timing.FrameStartTime;
        _impacts.RemoveAll(impact => (frame - impact.At).TotalSeconds >
            ShipShieldProjectorComponent.ImpactDuration * MathF.Sqrt(impact.Power));
        _predictedContacts.RemoveAll(contact => now - contact.At > ShipShieldProjectorComponent.ImpactHistoryLifetime);
        foreach (var (key, times) in _contactTimes)
        {
            if (now - times.Contact > ShipShieldProjectorComponent.PressureGrace)
                _contactTimes.Remove(key);
        }
        foreach (var uid in _lastImpactIds.Keys)
            if (!HasComp<ShipShieldProjectorComponent>(uid))
                _lastImpactIds.Remove(uid);

        foreach (var uid in _shatters.Keys)
            if (!HasComp<ShipShieldProjectorComponent>(uid))
                _shatters.Remove(uid);

        var query = EntityQueryEnumerator<ShipShieldProjectorComponent>();
        while (query.MoveNext(out var uid, out var shield))
        {
            UpdateColdStart(uid, shield, frame);
            if (shield.IsShattering)
                _shatters.TryAdd(uid, (frame, shield.StartedAt));
            else if (shield.State is not (ShipShieldShellState.Off or ShipShieldShellState.Standby) ||
                     _shatters.TryGetValue(uid, out var shatter) &&
                     frame - shatter.ObservedAt >= shield.AnimationDuration(ShipShieldShellState.Shattering))
                _shatters.Remove(uid);

            var lastId = _lastImpactIds.GetValueOrDefault(uid);
            foreach (var impact in shield.Impacts)
            {
                if (impact.Id <= lastId)
                    continue;

                lastId = impact.Id;
                if (now - impact.At > ShipShieldProjectorComponent.ImpactHistoryLifetime || ConfirmContact(uid, impact))
                    continue;

                _impacts.Add(new ImpactBurst(uid, impact.Origin, impact.Map, frame, impact.At, impact.Power));
            }

            if (lastId != 0)
                _lastImpactIds[uid] = lastId;
        }
    }

    internal bool TryGetShatterPhase(EntityUid uid, ShipShieldProjectorComponent shield, out float phase, out TimeSpan seed)
    {
        phase = 0f;
        seed = default;
        if (!_shatters.TryGetValue(uid, out var shatter))
            return false;

        var elapsed = _timing.FrameStartTime - shatter.ObservedAt;
        var duration = shield.AnimationDuration(ShipShieldShellState.Shattering);
        phase = 1f + Math.Clamp((float) (elapsed / duration), 0f, 1f);
        seed = shatter.StartedAt;
        return true;
    }

    internal readonly record struct ImpactBurst(EntityUid Shield, Vector3 Point, MapId Map, TimeSpan At, TimeSpan HitAt, float Power);
    private readonly record struct PredictedContact(EntityUid Shield, NetEntity Source, Vector3 Point, MapId Map, TimeSpan At);
}
