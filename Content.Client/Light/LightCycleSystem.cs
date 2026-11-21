using Content.Client.GameTicking.Managers;
using Content.Shared;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client.Light;

/// <inheritdoc/>
public sealed partial class LightCycleSystem : SharedLightCycleSystem
{
    [Dependency] private ClientGameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MetaDataSystem _metadata = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var mapQuery = AllEntityQuery<LightCycleComponent, MapLightComponent>();
        while (mapQuery.MoveNext(out var uid,  out var cycle, out var map))
        {
            if (!cycle.Running)
                continue;

            map.AmbientLightColor = SampleAmbient((uid, cycle));
        }
    }

    public Color SampleAmbient(EntityUid mapUid, MapLightComponent map) =>
        TryComp<LightCycleComponent>(mapUid, out var cycle) && cycle.Running
            ? SampleAmbient((mapUid, cycle))
            : map.AmbientLightColor;

    private Color SampleAmbient(Entity<LightCycleComponent> cycle)
    {
        var time = _timing.CurTime + cycle.Comp.Offset - _ticker.RoundStartTimeSpan - _metadata.GetPauseTime(cycle);
        return GetColor(cycle, cycle.Comp.OriginalColor, (float) time.TotalSeconds);
    }
}
