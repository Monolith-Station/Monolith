using Content.Shared._Mono.Traits.Physical;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Mono.Traits.Physical;

/// <summary>
/// Applies the Tenacity trait effects by increasing the critical health threshold.
/// </summary>
public sealed class TenacitySystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _mobThresholds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TenacityComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TenacityComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<TenacityComponent> ent, ref ComponentStartup args)
    {
        AdjustCritThreshold(ent.Owner, ent.Comp.CritIncrease);
    }

    private void OnShutdown(Entity<TenacityComponent> ent, ref ComponentShutdown args)
    {
        AdjustCritThreshold(ent.Owner, -ent.Comp.CritIncrease);
    }

    private void AdjustCritThreshold(EntityUid uid, int deltaPoints, MobThresholdsComponent? thresholdsComp = null)
    {
        if (!_mobThresholds.TryGetThresholdForState(uid, MobState.Critical, out var current, thresholdsComp))
            return;

        var newValue = FixedPoint2.Max(0, current.Value + (FixedPoint2)deltaPoints);

        _mobThresholds.SetMobStateThreshold(uid, newValue, MobState.Critical, thresholdsComp);
    }
}


