using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Map.Components;

namespace Content.Server.Light.EntitySystems;

public sealed partial class LightCycleSystem
{
    private void InitializeZLevels()
    {
        SubscribeLocalEvent<CEZMapNetworkComponent, ComponentStartup>(OnZNetworkStartup);
        SubscribeLocalEvent<LightCycleComponent, CEZLevelMapNetworkUpdatedEvent>(OnZNetworkUpdated);
        SubscribeLocalEvent<LightCycleComponent, ComponentStartup>(OnZCycleStartup);
        SubscribeLocalEvent<LightCycleComponent, LightCycleOffsetEvent>(OnZCycleOffset);
    }

    private void OnZNetworkStartup(Entity<CEZMapNetworkComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<LightCycleComponent>(ent, out var cycle))
            SyncZNetwork((ent, ent.Comp, cycle));
    }

    private void OnZNetworkUpdated(Entity<LightCycleComponent> ent, ref CEZLevelMapNetworkUpdatedEvent args)
    {
        if (TryComp<CEZMapNetworkComponent>(ent, out var network))
            SyncZNetwork((ent, network, ent.Comp));
    }

    private void OnZCycleStartup(Entity<LightCycleComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<CEZMapNetworkComponent>(ent, out var network))
            return;

        if (ent.Comp.InitialOffset)
        {
            ent.Comp.InitialOffset = false;
            SetOffset(ent, _random.Next(ent.Comp.Duration));
        }

        SyncZNetwork((ent, network, ent.Comp));
    }

    private void OnZCycleOffset(Entity<LightCycleComponent> ent, ref LightCycleOffsetEvent args)
    {
        if (TryComp<CEZMapNetworkComponent>(ent, out var network))
            SyncZNetwork((ent, network, ent.Comp));
    }

    private void SyncZNetwork(Entity<CEZMapNetworkComponent, LightCycleComponent> network)
    {
        foreach (var mapUid in network.Comp1.SortedZLevels)
        {
            if (!mapUid.IsValid() || TerminatingOrDeleted(mapUid))
                continue;

            var target = EnsureComp<LightCycleComponent>(mapUid);
            CopyCycle(network.Comp2, target);

            if (TryComp<MapLightComponent>(mapUid, out var mapLight))
                target.OriginalColor = mapLight.AmbientLightColor;

            Dirty(mapUid, target);

            if (TryComp<SunShadowCycleComponent>(mapUid, out var shadow))
            {
                shadow.Duration = target.Duration;
                shadow.Offset = target.Offset;
                Dirty(mapUid, shadow);
            }
        }
    }

    private static void CopyCycle(LightCycleComponent source, LightCycleComponent target)
    {
        target.Duration = source.Duration;
        target.Offset = source.Offset;
        target.Enabled = source.Enabled;
        target.InitialOffset = false;
        target.MinLightLevel = source.MinLightLevel;
        target.MaxLightLevel = source.MaxLightLevel;
        target.ClipLight = source.ClipLight;
        target.ClipLevel = source.ClipLevel;
        target.MinLevel = source.MinLevel;
        target.MaxLevel = source.MaxLevel;
    }
}
