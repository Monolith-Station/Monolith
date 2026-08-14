using Content.Server._Mono.GridClaimer;
using Content.Server._NF.Shuttles.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono.CCVar;
using Content.Shared.Shuttles.Components;
using Content.Shared.Tiles;
using Prometheus;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Cleanup;

// Forge-Change-Start: despawn shuttles that no player has boarded for an hour.

/// <summary>
///     Deletes abandoned shuttles that no player has boarded (or docked next to) for a configurable delay.
///     Stations, POIs (<see cref="ProtectedGridComponent"/>), claimed grids, and FTL are skipped.
/// </summary>
public sealed class ShuttleIdleCleanupSystem : EntitySystem
{
    private static readonly Counter Deleted = Metrics.CreateCounter(
        "shuttle_idle_cleanup_deleted",
        "Shuttles deleted after staying unoccupied.");

    private static readonly Gauge Tracked = Metrics.CreateGauge(
        "shuttle_idle_cleanup_tracked",
        "Shuttle grids currently tracked for idle cleanup.");

    [Dependency] private readonly CleanupHelperSystem _cleanup = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private bool _enabled = true;
    private TimeSpan _idleFor = TimeSpan.FromHours(1);
    private TimeSpan _scanInterval = TimeSpan.FromSeconds(15);
    private float _approachDistance = 48f;

    private TimeSpan _nextScan;
    private int _deletedThisRound;

    private readonly HashSet<EntityUid> _occupied = new();
    private readonly HashSet<EntityUid> _occupiedScratch = new();

    private EntityQuery<CleanupImmuneComponent> _immuneQuery;
    private EntityQuery<ClaimableGridComponent> _claimQuery;
    private EntityQuery<FTLComponent> _ftlQuery;
    private EntityQuery<FTLMapComponent> _ftlMapQuery;
    private EntityQuery<ForceAnchorComponent> _forceAnchorQuery;
    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<ProtectedGridComponent> _protectedQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private const int MaxDockExpandPasses = 8;
    private const int MaxDeletesPerScan = 4;

    public override void Initialize()
    {
        base.Initialize();

        _immuneQuery = GetEntityQuery<CleanupImmuneComponent>();
        _claimQuery = GetEntityQuery<ClaimableGridComponent>();
        _ftlQuery = GetEntityQuery<FTLComponent>();
        _ftlMapQuery = GetEntityQuery<FTLMapComponent>();
        _forceAnchorQuery = GetEntityQuery<ForceAnchorComponent>();
        _mapQuery = GetEntityQuery<MapComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _protectedQuery = GetEntityQuery<ProtectedGridComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        Subs.CVar(_cfg, MonoCVars.ShuttleIdleCleanupEnabled, val => _enabled = val, true);
        Subs.CVar(_cfg, MonoCVars.ShuttleIdleCleanupSeconds, val =>
        {
            _idleFor = TimeSpan.FromSeconds(Math.Max(val, 60f));
        }, true);
        Subs.CVar(_cfg, MonoCVars.ShuttleIdleCleanupScanSeconds, val =>
        {
            _scanInterval = TimeSpan.FromSeconds(Math.Max(val, 5f));
        }, true);
        Subs.CVar(_cfg, MonoCVars.ShuttleIdleCleanupApproachDistance, val => _approachDistance = val, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled || _timing.CurTime < _nextScan)
            return;

        _nextScan = _timing.CurTime + _scanInterval;
        Scan();
    }

    private void Scan()
    {
        BuildOccupiedGrids();

        var now = _timing.CurTime;
        var tracked = 0;
        var deleted = 0;

        var query = EntityQueryEnumerator<ShuttleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (IsExempt(uid, xform))
                continue;

            // Forge-Change: directed ComponentStartup is exclusive to ShuttleSystem; attach the timer here instead.
            var existed = EnsureComp(uid, out ShuttleIdleCleanupComponent idle);
            if (!existed)
                idle.LastOccupied = now;

            tracked++;

            if (IsOccupied(uid, xform))
            {
                idle.LastOccupied = now;
                continue;
            }

            if (deleted >= MaxDeletesPerScan || now - idle.LastOccupied < _idleFor)
                continue;

            Log.Info($"Idle-cleanup deleting {ToPrettyString(uid)} (empty for {(now - idle.LastOccupied).TotalMinutes:F0} min)");
            QueueDel(uid);
            Deleted.Inc();
            deleted++;
            _deletedThisRound++;
        }

        Tracked.Set(tracked);

        if (deleted > 0)
            Log.Info($"Idle-cleanup removed {deleted} shuttle(s) this scan ({_deletedThisRound} this round)");
    }

    private void BuildOccupiedGrids()
    {
        _occupied.Clear();
        _cleanup.CollectOccupiedGrids(_occupied);
        ExpandOccupiedViaDocks();
    }

    /// <summary>
    ///     If A is occupied and docked to B, B is occupied too (ship parked at an outpost).
    /// </summary>
    private void ExpandOccupiedViaDocks()
    {
        if (_occupied.Count == 0)
            return;

        for (var pass = 0; pass < MaxDockExpandPasses; pass++)
        {
            _occupiedScratch.Clear();
            var docks = EntityQueryEnumerator<DockingComponent, TransformComponent>();
            while (docks.MoveNext(out _, out var dock, out var xform))
            {
                if (dock.DockedWith is not { } other)
                    continue;

                var gridA = xform.GridUid;
                if (gridA == null || !_xformQuery.TryGetComponent(other, out var otherXform))
                    continue;

                var gridB = otherXform.GridUid;
                if (gridB == null)
                    continue;

                var aOcc = _occupied.Contains(gridA.Value);
                var bOcc = _occupied.Contains(gridB.Value);
                if (aOcc == bOcc)
                    continue;

                _occupiedScratch.Add(aOcc ? gridB.Value : gridA.Value);
            }

            if (_occupiedScratch.Count == 0)
                break;

            foreach (var grid in _occupiedScratch)
                _occupied.Add(grid);
        }
    }

    private bool IsOccupied(EntityUid uid, TransformComponent xform)
    {
        if (_occupied.Contains(uid))
            return true;

        return _approachDistance > 0f && _cleanup.HasNearbyPlayers(xform.Coordinates, _approachDistance);
    }

    private bool IsExempt(EntityUid uid, TransformComponent xform)
    {
        if (TerminatingOrDeleted(uid) || MetaData(uid).EntityPaused)
            return true;

        if (_mapQuery.HasComp(uid) || _immuneQuery.HasComp(uid) || _protectedQuery.HasComp(uid) || _forceAnchorQuery.HasComp(uid))
            return true;

        if (_ftlQuery.HasComp(uid))
            return true;

        if (_claimQuery.TryGetComponent(uid, out var claim) && claim.Claimed)
            return true;

        if (_gridQuery.HasComp(xform.ParentUid))
            return true;

        if (xform.MapUid is { } mapUid && _ftlMapQuery.HasComp(mapUid))
            return true;

        return false;
    }
}
// Forge-Change-End
