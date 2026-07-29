using Content.Shared._Mono.CCVar;
using Content.Server._Mono.GridClaimer; /// Forge-Change
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Cleanup;

public abstract partial class BaseCleanupSystem<TComp> : EntitySystem
    where TComp : IComponent
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] protected readonly CleanupHelperSystem CleanupHelper = default!;

    protected TimeSpan _cleanupInterval = TimeSpan.FromSeconds(300);
    protected TimeSpan _debugCleanupInterval = TimeSpan.FromSeconds(15);
    protected bool _doDebug;
    protected bool _doLog;

    private Queue<EntityUid> _checkQueue = new();

    private TimeSpan _nextCleanup = TimeSpan.Zero;
    private int _delCount = 0;
    // used to track when we should be cleaning up the next entry in our queue
    private TimeSpan _cleanupAccumulator = TimeSpan.Zero;
    private TimeSpan _cleanupDeferDuration;

    /// <summary>
    ///     Hard cap on how many queue items get processed in a single update tick.
    ///     Prevents catch-up loops from causing micro-spikes after long pauses.
    /// </summary>
    protected int MaxChecksPerTick = 16;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, MonoCVars.CleanupDebug, val => _doDebug = val, true);
        Subs.CVar(_cfg, MonoCVars.CleanupLog, val => _doLog = val, true);
        Subs.CVar(_cfg, MonoCVars.CleanupMaxChecksPerTick, val => MaxChecksPerTick = val, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // drain a bounded slice of the queue per tick
        if (_checkQueue.Count != 0)
        {
            _cleanupAccumulator += TimeSpan.FromSeconds(frameTime);
            var processed = 0;
            while (_cleanupAccumulator > _cleanupDeferDuration && processed < MaxChecksPerTick)
            {
                _cleanupAccumulator -= _cleanupDeferDuration;
                processed++;

                if (_checkQueue.Count == 0)
                    return;
                var uid = _checkQueue.Dequeue();
                if (TerminatingOrDeleted(uid))
                    continue;

                if (!ShouldEntityCleanup(uid))
                    continue;

                CleanupEnt(uid);
            }
            return;
        }

        if (_delCount != 0)
        {
            Log.Info($"Deleted {_delCount} entities");
            _delCount = 0;
        }

        // we appear to be done with previous queue so try get another
        var curTime = _timing.CurTime;
        if (curTime < _nextCleanup)
            return;
        var interval = !_doDebug ? _cleanupInterval : _debugCleanupInterval;
        _nextCleanup = curTime + interval;

        _checkQueue.Clear();

        // Forge-Change: time the scan and apply a cheap pre-filter before enqueue.
        // Building the queue is O(N) over all entities with TComp; filtering here keeps
        // the queue (and downstream ShouldEntityCleanup work) proportional to real candidates.
        var scanStart = _timing.RealTime;
        var seen = 0;
        var query = EntityQueryEnumerator<TComp>();
        while (query.MoveNext(out var uid, out _))
        {
            seen++;
            if (!ShouldEnqueue(uid))
                continue;
            _checkQueue.Enqueue(uid);
        }
        if (_checkQueue.Count != 0)
            _cleanupDeferDuration = interval * 0.9 / _checkQueue.Count;
        var scanMs = (_timing.RealTime - scanStart).TotalMilliseconds;

        Log.Debug(
            $"Ran cleanup queue, scanned {seen}, queued {_checkQueue.Count}, scan {scanMs:F1}ms, deleting over {_cleanupDeferDuration}");
    }

    protected void CleanupEnt(EntityUid uid)
    {
        // don't delete it if claimed
        if (TryComp<ClaimableGridComponent>(uid, out var claimable) && claimable.Claimed)   /// Forge-Change
            return;                                                                         /// Forge-Change
        var coord = Transform(uid).Coordinates;
        var world = _transform.ToMapCoordinates(coord);
        if (_doLog)
            Log.Debug($"Cleanup deleting entity {ToPrettyString(uid)} at {coord} (world {world})");

        _delCount += 1;
        QueueDel(uid);
    }

    /// <summary>
    ///     Cheap pre-filter applied at scan time, before an entity is enqueued for the slower
    ///     <see cref="ShouldEntityCleanup"/> pass. Override to skip obvious non-candidates
    ///     (wrong parent, immune marker, mind-bound, etc.) and avoid wasted dequeue work.
    /// </summary>
    protected virtual bool ShouldEnqueue(EntityUid uid) => true;

    protected abstract bool ShouldEntityCleanup(EntityUid uid);
}
