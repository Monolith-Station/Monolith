using Content.Shared._Mono.Traits.Physical;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Mono.Traits.Physical;

/// <summary>
/// Applies the Glass Jaw trait effects by adjusting the critical health threshold.
/// </summary>
public sealed class GlassJawSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _mobThresholds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GlassJawComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<GlassJawComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MobThresholdsComponent, ComponentInit>(OnMobThresholdsInit);
    }

    private void OnStartup(Entity<GlassJawComponent> ent, ref ComponentStartup args)
    {
        AdjustCritThreshold(ent.Owner, -ent.Comp.CritDecrease);
    }

    private void OnShutdown(Entity<GlassJawComponent> ent, ref ComponentShutdown args)
    {
        AdjustCritThreshold(ent.Owner, ent.Comp.CritDecrease);
    }

    private void OnMobThresholdsInit(EntityUid uid, MobThresholdsComponent comp, ComponentInit args)
    {
        if (HasComp<GlassJawComponent>(uid))
        {
            var gj = Comp<GlassJawComponent>(uid);
            AdjustCritThreshold(uid, -gj.CritDecrease, comp);
        }
    }

    private void AdjustCritThreshold(EntityUid uid, int deltaPoints, MobThresholdsComponent? thresholdsComp = null)
    {
        if (!_mobThresholds.TryGetThresholdForState(uid, MobState.Critical, out var current, thresholdsComp))
            return;

        var newValue = FixedPoint2.Max(0, current.Value + (FixedPoint2)deltaPoints);

        _mobThresholds.SetMobStateThreshold(uid, newValue, MobState.Critical, thresholdsComp);
    }
}
