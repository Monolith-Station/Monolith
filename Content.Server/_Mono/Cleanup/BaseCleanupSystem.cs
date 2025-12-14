using Content.Shared._Mono.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using System;

namespace Content.Server._Mono.Cleanup;

public abstract class BaseCleanupSystem<TComp> : EntitySystem
    where TComp : IComponent
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

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

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, MonoCVars.CleanupDebug, val => _doDebug = val, true);
        Subs.CVar(_cfg, MonoCVars.CleanupLog, val => _doLog = val, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // delete one queued entity per update
        if (_checkQueue.Count != 0)
        {
            _cleanupAccumulator += TimeSpan.FromSeconds(frameTime);
            while (_cleanupAccumulator > _cleanupDeferDuration)
            {
                _cleanupAccumulator -= _cleanupDeferDuration;

                if (_checkQueue.Count == 0)
                    return;
                var uid = _checkQueue.Dequeue();
                if (TerminatingOrDeleted(uid))
                    continue;

                if (!ShouldEntityCleanup(uid))
                    continue;

                if (_doLog)
                    Log.Info($"Cleanup deleting entity {ToPrettyString(uid)}");

                _delCount += 1;
                QueueDel(uid);
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
        // queue the next batch
        var query = EntityQueryEnumerator<TComp>();
        while (query.MoveNext(out var uid, out _))
        {
            _checkQueue.Enqueue(uid);
        }
        if (_checkQueue.Count != 0)
            _cleanupDeferDuration = interval * 0.9 / _checkQueue.Count;

        Log.Debug($"Ran cleanup queue, found: {_checkQueue.Count}, deleting over {_cleanupDeferDuration}");
    }

    protected abstract bool ShouldEntityCleanup(EntityUid uid);
}
