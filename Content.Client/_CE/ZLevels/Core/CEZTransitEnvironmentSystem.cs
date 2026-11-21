using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.Map.Components;

namespace Content.Client._CE.ZLevels.Core;

public sealed partial class CEZTransitEnvironmentSystem : EntitySystem
{
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private Content.Client.Light.LightCycleSystem _lightCycle = default!;

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CEZTransitMapComponent, MapLightComponent>();
        while (query.MoveNext(out _, out var transit, out var light))
        {
            var lower = SampleAmbient(transit.LowerMap);
            var upper = SampleAmbient(transit.UpperMap);
            if (lower == null && upper == null)
                continue;

            light.AmbientLightColor = Color.InterpolateBetween(
                lower ?? upper!.Value,
                upper ?? lower!.Value,
                _zLevels.GetTransitProgress(transit));
        }
    }

    private Color? SampleAmbient(EntityUid? map) =>
        map is { } uid && TryComp<MapLightComponent>(uid, out var light)
            ? _lightCycle.SampleAmbient(uid, light)
            : null;
}
