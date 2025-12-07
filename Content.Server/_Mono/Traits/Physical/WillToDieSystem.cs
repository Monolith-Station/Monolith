using Content.Shared._Mono.Traits.Physical;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Mono.Traits.Physical;

/// <summary>
/// Applies the Will To Die trait effects by decreasing the death health threshold.
/// </summary>
public sealed class WillToDieSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _mobThresholds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WillToDieComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WillToDieComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<WillToDieComponent> ent, ref ComponentStartup args)
    {
        AdjustDeathThreshold(ent.Owner, -ent.Comp.DeadDecrease);
    }

    private void OnShutdown(Entity<WillToDieComponent> ent, ref ComponentShutdown args)
    {
        AdjustDeathThreshold(ent.Owner, ent.Comp.DeadDecrease);
    }

    private void AdjustDeathThreshold(EntityUid uid, int deltaPoints, MobThresholdsComponent? thresholdsComp = null)
    {
        if (!_mobThresholds.TryGetThresholdForState(uid, MobState.Dead, out var current, thresholdsComp))
            return;

        var newValue = FixedPoint2.Max(0, current.Value + deltaPoints);

        _mobThresholds.SetMobStateThreshold(uid, newValue, MobState.Dead, thresholdsComp);
    }
}


