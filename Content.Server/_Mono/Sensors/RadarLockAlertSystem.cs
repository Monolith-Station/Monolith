using Content.Shared._Mono.Sensors;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Sensors;

public sealed class RadarLockAlertSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RadarLockSystem _radarLock = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _nextBeep = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RadarLockAcquiredEvent>(OnLock);
    }

    private void OnLock(RadarLockAcquiredEvent ev)
    {
        if (!TryComp<ConsoleRadarLockToneComponent>(ev.Console, out var tone))
            return;

        if (_nextBeep.TryGetValue(ev.Console, out var next) && _timing.CurTime < next)
            return;

        _nextBeep[ev.Console] = _timing.CurTime + tone.Cooldown;

        var chaffed = _radarLock.IsBearingChaffedForConsole(ev.Console, ev.TargetGrid);
        var volume = chaffed ? tone.LockAcquiredSound.Params.Volume - 4f : tone.LockAcquiredSound.Params.Volume;

        _audio.PlayPvs(tone.LockAcquiredSound, ev.TargetGrid,
            AudioParams.Default.WithVolume(volume));
    }
}
