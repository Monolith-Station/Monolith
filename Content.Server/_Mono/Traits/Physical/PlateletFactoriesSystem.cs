using Content.Shared._Mono.Traits.Physical;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Traits.Physical;

/// <summary>
/// Applies slow, uncapped regeneration over all existing damage types for entities with PlateletFactoriesComponent.
/// </summary>
public sealed class PlateletFactoriesSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlateletFactoriesComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<PlateletFactoriesComponent> ent, ref ComponentInit args)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(0.1f, ent.Comp.IntervalSeconds));
        ent.Comp.NextUpdate = _timing.CurTime + interval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<PlateletFactoriesComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextUpdate > curTime)
                continue;

            var interval = TimeSpan.FromSeconds(Math.Max(0.1f, comp.IntervalSeconds));
            comp.NextUpdate += interval;

            Tick(uid, comp);
        }
    }

    private void Tick(EntityUid uid, PlateletFactoriesComponent comp)
    {
        if (!TryComp<DamageableComponent>(uid, out var damage))
            return;

        if (damage.TotalDamage <= 0)
            return;

        if (TryComp<MobStateComponent>(uid, out var mobState) && _mobState.IsDead(uid, mobState))
            return;

        var heal = new DamageSpecifier();

        var amountPerTick = Math.Max(0f, comp.HealPerSecond) * Math.Max(0.1f, comp.IntervalSeconds);
        var multiplier = (TryComp<MobStateComponent>(uid, out var ms) && _mobState.IsCritical(uid, ms))
            ? comp.CritMultiplier
            : 1f;

        foreach (var (type, amount) in damage.Damage.DamageDict)
        {
            if (amount <= 0)
                continue;

            var healAmt = FixedPoint2.New(-Math.Min(amount.Float(), amountPerTick * multiplier));
            if (healAmt == FixedPoint2.Zero)
                continue;

            var existing = heal.DamageDict.GetValueOrDefault(type);
            heal.DamageDict[type] = existing + healAmt;
        }

        if (heal.DamageDict.Count == 0)
            return;

        _damageable.TryChangeDamage(uid, heal, true, false, damage);
    }
}


