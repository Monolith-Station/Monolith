using Content.Shared._Mono.Humanoid;
using Content.Shared._Mono.Traits.Physical;

namespace Content.Server._Mono.Traits.Physical;

/// <summary>
/// Applies the Will To Live trait effects by modifying the death health threshold.
/// </summary>
public sealed class ThresholdOffsetSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ThresholdOffsetComponent, QueryMobThresholdsEvent>(OnQueryMobThresholds);
    }

    private void OnQueryMobThresholds(EntityUid uid, ThresholdOffsetComponent component, QueryMobThresholdsEvent ev)
    {
        ev.DeathOffset += component.DeadOffset;
        ev.CritOffset += component.CritOffset;
    }
}



