using Content.Shared._Mono.Sensors;
using Robust.Server.Audio;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Sensors;

public sealed partial class RadarLockAlertSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _nextBeep = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RadarLockAcquiredEvent>(OnLock);
    }

    private void OnLock(RadarLockAcquiredEvent ev)
    {
        var targetGrid = ev.TargetGrid;

        var q = EntityQueryEnumerator<ConsoleRadarLockToneComponent, TransformComponent>();
        while (q.MoveNext(out var consoleUid, out var tone, out var xform))
        {
            if (xform.GridUid != targetGrid)
                continue;

            if (_nextBeep.TryGetValue(consoleUid, out var next) && _timing.CurTime < next)
                continue;

            _nextBeep[consoleUid] = _timing.CurTime + tone.Cooldown;

            _audio.PlayPvs(tone.LockAcquiredSound, consoleUid);
        }
    }
}
