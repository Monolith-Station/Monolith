using Content.Client._ES.Lighting.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._ES.Lighting;

public sealed class ESGenericPointLightVisualizerSystem : VisualizerSystem<ESGenericPointLightVisualizerComponent>
{
    [Dependency] private readonly PointLightSystem _pointLight = default!;

    protected override void OnAppearanceChange(EntityUid uid, ESGenericPointLightVisualizerComponent component, ref AppearanceChangeEvent args)
    {
        if (!TryComp<PointLightComponent>(uid, out var pointLight))
        {
            throw new Exception($"Entity {ToPrettyString(uid)} with {nameof(ESGenericPointLightVisualizerComponent)} does not have {nameof(PointLightComponent)}!");
        }
        DebugTools.Assert(!pointLight.NetSyncEnabled, $"Entity {ToPrettyString(uid)} uses point light visuals with a netsync'd pointlight! (Did you forget to set netsync: false?)");

        foreach (var (appearanceKey, layerDataDict) in component.Visuals)
        {
            if (!AppearanceSystem.TryGetData(uid, appearanceKey, out var obj, args.Component))
                continue;

            var appearanceValue = obj.ToString();
            if (string.IsNullOrEmpty(appearanceValue))
                continue;

            if (!layerDataDict.TryGetValue(appearanceValue, out var layerData))
                continue;

            layerData.Apply((uid, pointLight), _pointLight);
        }
    }
}
