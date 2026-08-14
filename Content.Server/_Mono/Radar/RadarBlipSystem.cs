using System.Numerics;
using Content.Shared._Mono.CCVar; // Forge-Change
using Content.Server._Mono.Projectiles.TargetGuided;
using Content.Server._Mono.Projectiles.TargetSeeking;
using Content.Shared._Mono.Radar;
using Content.Shared.Projectiles;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Configuration; // Forge-Change
using Robust.Shared.Enums; // Forge-Change: prune radar request throttle on disconnect
using Robust.Shared.Map;
using Robust.Shared.Player; // Forge-Change
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Robust.Server.Player; // Forge-Change

namespace Content.Server._Mono.Radar;

public sealed partial class RadarBlipSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IConfigurationManager _cfg = default!; // Forge-Change
    [Dependency] private IGameTiming _timing = default!; // Forge-Change
    [Dependency] private IPlayerManager _playerManager = default!; // Forge-Change

    // Pooled collections to avoid per-request heap churn
    private readonly List<BlipNetData> _tempBlipsCache = new();
    private readonly List<MissileVectorNetData> _tempMissileCache = new();
    private readonly List<HitscanNetData> _tempHitscansCache = new();
    private readonly List<EntityUid> _tempSourcesCache = new();
    private readonly List<Vector2> _tempSourcePositionsCache = new(); // Forge-Change: precomputed source world positions
    private readonly List<BlipConfig> _tempPaletteCache = new();
    private readonly Dictionary<BlipConfig, ushort> _paletteIndex = new();
    private readonly Dictionary<NetUserId, TimeSpan> _lastRequestByUser = new(); // Forge-Change

    // Per-request grid xform/body cache so blips parented to the same grid don't re-resolve it
    private readonly Dictionary<EntityUid, GridFrameCache> _gridFrameCache = new();

    private sealed class GridFrameCache
    {
        public Angle LocalRotation;
        public PhysicsComponent? Body;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestBlipsEvent>(OnBlipsRequested);
        SubscribeLocalEvent<RadarBlipComponent, ComponentShutdown>(OnBlipShutdown);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged; // Forge-Change
    }

    // Forge-Change-Start: _lastRequestByUser grew forever across reconnects.
    public override void Shutdown()
    {
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        base.Shutdown();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.Disconnected)
            return;

        _lastRequestByUser.Remove(e.Session.UserId);
    }
    // Forge-Change-End

    private void OnBlipsRequested(RequestBlipsEvent ev, EntitySessionEventArgs args)
    {
        // Forge-Change-start
        var userId = args.SenderSession.UserId;
        var now = _timing.CurTime;
        var delay = TimeSpan.FromSeconds(_cfg.GetCVar(MonoCVars.RadarBlipRequestDelay));
        if (_lastRequestByUser.TryGetValue(userId, out var lastRequest) &&
            now - lastRequest < delay)
        {
            return;
        }
        _lastRequestByUser[userId] = now;
        // Forge-Change-end
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

        // Precompute source world positions once instead of recomputing per blip.
        _tempSourcePositionsCache.Clear();
        foreach (var source in _tempSourcesCache)
            _tempSourcePositionsCache.Add(_xform.GetWorldPosition(source));

        AssembleBlipsReport((EntityUid)radarUid, _tempSourcePositionsCache, radar);
        AssembleHitscanReport((EntityUid)radarUid, _tempSourcePositionsCache, radar);

        // Combine the blips and hitscan lines
        var giveEv = new GiveBlipsEvent(_tempPaletteCache, _tempBlipsCache, _tempMissileCache, _tempHitscansCache);
        RaiseNetworkEvent(giveEv, args.SenderSession);

        _tempBlipsCache.Clear();
        _tempMissileCache.Clear();
        _tempHitscansCache.Clear();
        _tempSourcesCache.Clear();
        _tempSourcePositionsCache.Clear();
        _tempPaletteCache.Clear();
        _paletteIndex.Clear();
        _gridFrameCache.Clear();
    }

    private void OnBlipShutdown(EntityUid blipUid, RadarBlipComponent component, ComponentShutdown args)
    {
        if (!TryComp<TransformComponent>(blipUid, out var blipXform))
            return;

        var netBlipUid = GetNetEntity(blipUid);
        var removalEv = new BlipRemovalEvent(netBlipUid);
        // Match blip visibility radius (MaxDistance from radar sources), not Filter.Pvs default (~50),
        // so clients who still have this blip in their list always get removal on the same map.
        var mapCoords = _xform.GetMapCoordinates(blipUid, blipXform);
        RaiseNetworkEvent(
            removalEv,
            Filter.Empty().AddInRange(mapCoords, component.MaxDistance, _playerManager, EntityManager));
    }

    private void AssembleBlipsReport(EntityUid uid, List<Vector2> sourcePositions, RadarConsoleComponent? component = null)
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
                || !NearAnySources(_xform.GetWorldPosition(blipXform), sourcePositions, blip.MaxDistance)
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
                var gridFrame = GetGridFrame(blipGrid.Value);
                if (gridFrame.Body != null) // prevent log spam
                    blipVelocity -= _physics.GetLinearVelocity(blipGrid.Value, coord.Position, gridFrame.Body);
                // it's local-frame velocity so rotate it too
                blipVelocity = (-gridFrame.LocalRotation).RotateVec(blipVelocity);
                // and also offset the rotation
                rotation -= gridFrame.LocalRotation;
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

        var missileQuery = EntityQueryEnumerator<TargetSeekingComponent, RadarBlipComponent, TransformComponent>();
        while (missileQuery.MoveNext(out var missile, out var seeker, out var missileBlip, out var missileBlipXform))
        {
            var netMissileUid = GetNetEntity(missile);
            var missileArc = MathHelper.DegreesToRadians(seeker.ScanArc);
            if (seeker.ArcLines)
                _tempMissileCache.Add(new(netMissileUid,
                    (float)(seeker.MaxSpeed * 0.2),
                    missileArc));
        }
        // Check for SACLOS/mouse-guided missiles to add their directional lines to the console.
        var saclosQuery = EntityQueryEnumerator<TargetGuidedComponent, RadarBlipComponent, TransformComponent>();
        while (saclosQuery.MoveNext(out var missile, out var seeker, out var missileBlip, out var missileBlipXform))
        {
            var netMissileUid = GetNetEntity(missile);
            if (seeker.RadarLines)
                _tempMissileCache.Add(new(netMissileUid,
                    (float)(seeker.CurrentSpeed * 0.2),
                    0));
        }
    }

    private GridFrameCache GetGridFrame(EntityUid grid)
    {
        if (_gridFrameCache.TryGetValue(grid, out var cached))
            return cached;

        var gridXform = Transform(grid);
        TryComp<PhysicsComponent>(grid, out var gridBody);
        cached = new GridFrameCache { LocalRotation = gridXform.LocalRotation, Body = gridBody };
        _gridFrameCache[grid] = cached;
        return cached;
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
    private void AssembleHitscanReport(EntityUid uid, List<Vector2> sourcePositions, RadarConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var hitscanQuery = EntityQueryEnumerator<HitscanRadarComponent>();

        while (hitscanQuery.MoveNext(out var hitscanUid, out var hitscan))
        {
            if (!hitscan.Enabled)
                continue;

            // Skip only when neither endpoint is in range of any radar source.
            if (!NearAnySources(hitscan.StartPosition, sourcePositions, component.MaxRange)
                && !NearAnySources(hitscan.EndPosition, sourcePositions, component.MaxRange))
                continue;

            _tempHitscansCache.Add(new(hitscan.StartPosition, hitscan.EndPosition, hitscan.LineThickness, hitscan.RadarColor));
        }
    }

    private static bool NearAnySources(Vector2 coord, List<Vector2> sourcePositions, float range)
    {
        var rsqr = range * range;
        for (var i = 0; i < sourcePositions.Count; i++)
        {
            if ((sourcePositions[i] - coord).LengthSquared() < rsqr)
                return true;
        }
        return false;
    }
}
