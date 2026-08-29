using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.Map.Components;

namespace Content.Client._CE.ZLevels.Core;

public sealed partial class CEZTransitEnvironmentSystem : EntitySystem
{
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(Content.Client.Light.LightCycleSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CEZTransitMapComponent, MapLightComponent>();
        while (query.MoveNext(out _, out var transit, out var light))
        {
            if (transit.LowerMap is not { } lower ||
                !TryComp<MapLightComponent>(lower, out var lowerLight))
                continue;

            var upperColor = transit.UpperMap is { } upper && TryComp<MapLightComponent>(upper, out var upperLight)
                ? upperLight.AmbientLightColor
                : lowerLight.AmbientLightColor;

            light.AmbientLightColor = Color.InterpolateBetween(
                lowerLight.AmbientLightColor,
                upperColor,
                _zLevels.GetTransitProgress(transit));
        }
    }
}
