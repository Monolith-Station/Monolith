using System.Numerics;
using Content.Shared._Mono.Radar;
using Content.Shared.Projectiles;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Mono.Radar;

public sealed partial class RadarBlipSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    // Pooled collections to avoid per-request heap churn
    private readonly List<BlipNetData> _tempBlipsCache = new();
    private readonly List<HitscanNetData> _tempHitscansCache = new();
    private readonly List<EntityUid> _tempSourcesCache = new();
    private readonly List<BlipConfig> _tempPaletteCache = new();
    private readonly Dictionary<BlipConfig, ushort> _paletteIndex = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestBlipsEvent>(OnBlipsRequested);
        SubscribeLocalEvent<RadarBlipComponent, ComponentShutdown>(OnBlipShutdown);
    }

    private void OnBlipsRequested(RequestBlipsEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetEntity(ev.Radar, out var radarUid)
            || !TryComp<RadarConsoleComponent>(radarUid, out var radar)
        )
            return;

        var sourcesEv = new GetRadarSourcesEvent();
        RaiseLocalEvent(radarUid.Value, ref sourcesEv);

        // Reuse pooled sources list
        _tempSourcesCache.Clear();
        if (sourcesEv.Sources != null)
            _tempSourcesCache.AddRange(sourcesEv.Sources);
        else
            _tempSourcesCache.Add(radarUid.Value);

        AssembleBlipsReport((EntityUid)radarUid, _tempSourcesCache, radar);
        AssembleHitscanReport((EntityUid)radarUid, _tempSourcesCache, radar);

        // Combine the blips and hitscan lines
        var giveEv = new GiveBlipsEvent(_tempPaletteCache, _tempBlipsCache, _tempHitscansCache);
        RaiseNetworkEvent(giveEv, args.SenderSession);

        _tempBlipsCache.Clear();
        _tempHitscansCache.Clear();
        _tempSourcesCache.Clear();
        _tempPaletteCache.Clear();
        _paletteIndex.Clear();
    }

    private void OnBlipShutdown(EntityUid blipUid, RadarBlipComponent component, ComponentShutdown args)
    {
        var netBlipUid = GetNetEntity(blipUid);
        var removalEv = new BlipRemovalEvent(netBlipUid);
        RaiseNetworkEvent(removalEv);
    }

    private void AssembleBlipsReport(EntityUid uid, List<EntityUid> sources, RadarConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var radarXform = Transform(uid);
        var radarGrid = radarXform.GridUid;
        var radarMapId = radarXform.MapID;

        var blipQuery = EntityQueryEnumerator<RadarBlipComponent, TransformComponent, PhysicsComponent>();

        while (blipQuery.MoveNext(out var blipUid, out var blip, out var blipXform, out var blipPhysics))
        {
            if (!blip.Enabled
                || blipXform.MapID != radarMapId
                || !NearAnySources(_xform.GetWorldPosition(blipXform), sources, blip.MaxDistance)
            )
                continue;

            var blipGrid = blipXform.GridUid;

            if (blip.RequireNoGrid && blipGrid != null // if we want no grid but we are on a grid
                || !blip.VisibleFromOtherGrids && blipGrid != radarGrid // or if we don't want to be visible from other grids but we're on another grid
            )
                continue; // don't show this blip

            var netBlipUid = GetNetEntity(blipUid);

            var blipVelocity = _physics.GetMapLinearVelocity(blipUid, blipPhysics, blipXform);

            // due to PVS being a thing, things will break if we try to parent to not the map or a grid
            var coord = blipXform.Coordinates;
            if (blipXform.ParentUid != blipXform.MapUid && blipXform.ParentUid != blipGrid)
                coord = _xform.WithEntityId(coord, blipGrid ?? blipXform.MapUid!.Value);

            var gridCfg = (BlipConfig?)null;
            var rotation = _xform.GetWorldRotation(blipXform);

            // we're parented to either the map or a grid and this is relative velocity so account for grid movement
            if (blipGrid != null)
            {
                var gridXform = Transform(blipGrid.Value);
                if (TryComp<PhysicsComponent>(blipGrid.Value, out var gridBody)) // prevent log spam
                    blipVelocity -= _physics.GetLinearVelocity(blipGrid.Value, coord.Position, gridBody);
                // it's local-frame velocity so rotate it too
                blipVelocity = (-gridXform.LocalRotation).RotateVec(blipVelocity);
                // and also offset the rotation
                rotation -= gridXform.LocalRotation;
                // and hijack our shape if we want to
                gridCfg = blip.GridConfig;
            }

            var configIdx = GetOrAddConfig(blip.Config);
            ushort? gridConfigIdx = gridCfg is { } gridCf ? GetOrAddConfig(gridCf) : null;

            // ideally we would handle blips being culled by detection on server but detection grid culling is already clientside so might as well
            _tempBlipsCache.Add(new(netBlipUid,
                            GetNetCoordinates(coord),
                            blipVelocity,
                            rotation,
                            configIdx,
                            gridConfigIdx));
        }
    }

    /// <summary>
    /// Gets or create palette index for blip config.
    /// </summary>
    private ushort GetOrAddConfig(BlipConfig config)
    {
        if (_paletteIndex.TryGetValue(config, out var index))
            return index;

        if (_tempPaletteCache.Count >= ushort.MaxValue)
        {
            Log.Error($"Blip config count overflow! Reached max {ushort.MaxValue}, but trying to add more.");
            return 0;
        }

        index = (ushort)_tempPaletteCache.Count;
        _tempPaletteCache.Add(config);
        _paletteIndex[config] = index;
        return index;
    }

    /// <summary>
    /// Assembles trajectory information for hitscan projectiles to be displayed on radar
    /// </summary>
    private void AssembleHitscanReport(EntityUid uid, List<EntityUid> sources, RadarConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var radarXform = Transform(uid);

        var hitscanQuery = EntityQueryEnumerator<HitscanRadarComponent>();

        while (hitscanQuery.MoveNext(out var hitscanUid, out var hitscan))
        {
            if (!hitscan.Enabled)
                continue;

            if (!NearAnySources(hitscan.StartPosition, sources, component.MaxRange) && NearAnySources(hitscan.EndPosition, sources, component.MaxRange))
                continue;

            _tempHitscansCache.Add(new(hitscan.StartPosition, hitscan.EndPosition, hitscan.LineThickness, hitscan.RadarColor));
        }
    }

    private bool NearAnySources(Vector2 coord, List<EntityUid> sources, float range)
    {
        var rsqr = range * range;
        foreach (var source in sources)
        {
            var pos = _xform.GetWorldPosition(source);
            if ((pos - coord).LengthSquared() < rsqr)
                return true;
        }
        return false;
    }
}
