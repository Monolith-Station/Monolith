using Content.Shared._Mono.Traits.Physical;
using Content.Shared.Damage;
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
        if (!TryComp<DamageableComponent>(uid, out var damage)
            || damage.TotalDamage <= 0
            || _mobState.IsDead(uid))
            return;

        var multiplier = _mobState.IsCritical(uid) ? comp.CritMultiplier : 1f;
        _damageable.TryChangeDamage(uid, comp.DamagePerInterval*multiplier, true, false, damage);
    }
}


