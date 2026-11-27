using Content.Server._Mono.Projectiles.TargetGuided;
using Content.Shared._Mono.FireControl;
using Content.Shared._Mono.Sensors;
using Content.Shared.Projectiles;
using Robust.Server.Audio;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Sensors;

public sealed partial class RadarLockAlertSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _nextBeep = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextMissileTone = new();

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

    public override void Update(float frameTime)
    {
        var seenTargets = new HashSet<EntityUid>();

        var mq = EntityQueryEnumerator<TargetGuidedComponent, ProjectileComponent, TransformComponent>();
        while (mq.MoveNext(out _, out var guided, out _, out _))
        {
            if (!guided.UseRadarTrack || guided.ControlPermanentlyLost)
                continue;
            if (guided.ControllingConsole is not { } c)
                continue;
            if (!TryComp<FireControlConsoleComponent>(c, out var console))
                continue;
            if (console.LockedTarget is not { } target)
                continue;

            var grid = Transform(target).GridUid ?? target;
            if (!seenTargets.Add(grid))
                continue;

            var toneQ = EntityQueryEnumerator<ConsoleRadarLockToneComponent, TransformComponent>();
            while (toneQ.MoveNext(out var consoleUid, out var comp, out var xform))
            {
                if (xform.GridUid != grid)
                    continue;
                if (_nextMissileTone.TryGetValue(consoleUid, out var next) && _timing.CurTime < next)
                    continue;

                _nextMissileTone[consoleUid] = _timing.CurTime + TimeSpan.FromSeconds(2.5);
                _audio.PlayPvs(comp.MissileTone, consoleUid);
            }
        }
    }
}
