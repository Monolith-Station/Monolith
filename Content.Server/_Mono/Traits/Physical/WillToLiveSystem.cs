using Content.Shared._Mono.Traits.Physical;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Mono.Traits.Physical;

/// <summary>
/// Applies the Will To Live trait effects by increasing the death health threshold.
/// </summary>
public sealed class WillToLiveSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _mobThresholds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WillToLiveComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WillToLiveComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<WillToLiveComponent> ent, ref ComponentStartup args)
    {
        AdjustDeathThreshold(ent.Owner, ent.Comp.DeadIncrease);
    }

    private void OnShutdown(Entity<WillToLiveComponent> ent, ref ComponentShutdown args)
    {
        AdjustDeathThreshold(ent.Owner, -ent.Comp.DeadIncrease);
    }

    private void AdjustDeathThreshold(EntityUid uid, int deltaPoints, MobThresholdsComponent? thresholdsComp = null)
    {
        if (!_mobThresholds.TryGetThresholdForState(uid, MobState.Dead, out var current, thresholdsComp))
            return;

        var newValue = FixedPoint2.Max(0, current.Value + deltaPoints);

        _mobThresholds.SetMobStateThreshold(uid, newValue, MobState.Dead, thresholdsComp);
    }
}



