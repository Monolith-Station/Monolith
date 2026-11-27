using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;

namespace Content.Server.Light.EntitySystems;

public sealed partial class LightCycleSystem
{
    private readonly Dictionary<EntityUid, CycleSettings> _syncedCycles = new();

    private void InitializeZLevels()
    {
        SubscribeLocalEvent<CEZMapNetworkComponent, ComponentStartup>(OnZNetworkStartup);
        SubscribeLocalEvent<CEZMapNetworkComponent, ComponentShutdown>(OnZNetworkShutdown);
        SubscribeLocalEvent<LightCycleComponent, CEZLevelMapNetworkUpdatedEvent>(OnZNetworkUpdated);
        SubscribeLocalEvent<LightCycleComponent, ComponentStartup>(OnZCycleStartup);
        SubscribeLocalEvent<LightCycleComponent, LightCycleOffsetEvent>(OnZCycleOffset);
    }

    private void OnZNetworkShutdown(Entity<CEZMapNetworkComponent> ent, ref ComponentShutdown args) =>
        _syncedCycles.Remove(ent.Owner);

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
        var settings = CycleSettings.Read(network.Comp2);
        _syncedCycles[network.Owner] = settings;
        foreach (var mapUid in network.Comp1.SortedZLevels)
        {
            if (mapUid.IsValid() && !TerminatingOrDeleted(mapUid))
                SyncMap(mapUid, settings);
        }

        var transits = AllEntityQuery<CEZTransitMapComponent>();
        while (transits.MoveNext(out var mapUid, out var transit))
        {
            if (!TerminatingOrDeleted(mapUid) && TryGetTransitNetwork(transit, out var networkUid) &&
                networkUid == network.Owner)
                SyncMap(mapUid, settings);
        }
    }

    internal void SyncTransitMap(Entity<CEZTransitMapComponent> transit, Color ambient)
    {
        if (!TryGetTransitNetwork(transit.Comp, out var network) ||
            !TryComp<LightCycleComponent>(network, out var cycle))
            return;

        SyncMap(transit.Owner, CycleSettings.Read(cycle), ambient);
    }

    private bool TryGetTransitNetwork(CEZTransitMapComponent transit, out EntityUid network)
    {
        if (TryComp<CEZMapComponent>(transit.LowerMap, out var anchor) ||
            TryComp(transit.UpperMap, out anchor))
        {
            network = anchor.NetworkUid;
            return true;
        }

        network = default;
        return false;
    }

    private void SyncMap(EntityUid map, CycleSettings settings, Color? ambient = null)
    {
        var target = EnsureComp<LightCycleComponent>(map);
        if (target.InitialOffset || CycleSettings.Read(target) != settings || ambient is { } baseColor && target.OriginalColor != baseColor)
        {
            settings.Apply(target);
            target.OriginalColor = ambient ?? target.OriginalColor;
            Dirty(map, target);
        }

        if (TryComp<SunShadowCycleComponent>(map, out var shadow) &&
            (shadow.Duration != settings.Duration || shadow.Offset != settings.Offset))
        {
            shadow.Duration = settings.Duration;
            shadow.Offset = settings.Offset;
            Dirty(map, shadow);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // VV writes bypass SetOffset. Compare settings, not per-map ambient colors.
        var networks = AllEntityQuery<CEZMapNetworkComponent>();
        while (networks.MoveNext(out var uid, out var network))
        {
            TryComp<LightCycleComponent>(uid, out var cycle);
            if (cycle != null && (!_syncedCycles.TryGetValue(uid, out var synced) || synced != CycleSettings.Read(cycle)))
            {
                Dirty(uid, cycle);
                SyncZNetwork((uid, network, cycle));
                continue;
            }

            foreach (var mapUid in network.SortedZLevels)
            {
                if (!TryComp<LightCycleComponent>(mapUid, out var layer))
                    continue;

                var settings = CycleSettings.Read(layer);
                if (cycle == null)
                {
                    var newCycle = new LightCycleComponent();
                    settings.Apply(newCycle);
                    AddComp(uid, newCycle);
                    break;
                }

                if (settings == CycleSettings.Read(cycle))
                    continue;

                settings.Apply(cycle);
                Dirty(uid, cycle);
                SyncZNetwork((uid, network, cycle));
                break;
            }
        }
    }

    private readonly record struct CycleSettings(TimeSpan Duration, TimeSpan Offset, bool Enabled,
        float MinLight, float MaxLight, float ClipLight, Color MinColor, Color MaxColor, Color ClipColor)
    {
        public static CycleSettings Read(LightCycleComponent cycle) =>
            new(cycle.Duration, cycle.Offset, cycle.Enabled, cycle.MinLightLevel, cycle.MaxLightLevel,
                cycle.ClipLight, cycle.MinLevel, cycle.MaxLevel, cycle.ClipLevel);

        public void Apply(LightCycleComponent cycle)
        {
            cycle.Duration = Duration;
            cycle.Offset = Offset;
            cycle.Enabled = Enabled;
            cycle.InitialOffset = false;
            cycle.MinLightLevel = MinLight;
            cycle.MaxLightLevel = MaxLight;
            cycle.ClipLight = ClipLight;
            cycle.MinLevel = MinColor;
            cycle.MaxLevel = MaxColor;
            cycle.ClipLevel = ClipColor;
        }
    }
}
