using Content.Server._Mono.Spawning;
using Content.Server.Power.Components;
using Content.Shared._Mono.Mining;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Mining;

public sealed partial class OreDrillSystem : EntitySystem
{
    [Dependency] private BuriedOreSystem _deposits = default!;
    [Dependency] private SpawnCountSystem _spawn = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    private readonly Dictionary<BuriedOreVein, List<EntityUid>> _groups = new();
    private readonly Stack<List<EntityUid>> _pool = new();
    private float _timer;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OreDrillComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<OreDrillComponent> ent, ref ComponentShutdown args)
        => StopAudio(ent.Comp);

    private void StopAudio(OreDrillComponent drill)
    {
        _audio.Stop(drill.Audio);
        drill.Audio = null;
    }

    public override void Update(float frameTime)
    {
        _timer += frameTime;
        if (_timer < 1f)
            return;
        var elapsed = Math.Min(_timer, 2f);
        _timer = 0f;

        var query = EntityQueryEnumerator<OreDrillComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var drill, out var xform))
        {
            BuriedOreVein? vein = null;
            if (xform.MapUid is { } surface && xform.GridUid == surface)
                vein = _deposits.GetVein(surface, _deposits.GetPosition(uid, surface));
            if (drill.Deposit != vein?.Mineral)
            {
                drill.Deposit = vein?.Mineral;
                Dirty(uid, drill);
            }
            if (!xform.Anchored || vein == null || vein.Remaining == 0 ||
                !TryComp<PowerConsumerComponent>(uid, out var power) || power.ReceivedPower < power.DrawRate)
            {
                StopAudio(drill);
                continue;
            }
            if (TerminatingOrDeleted(drill.Audio))
                drill.Audio = _audio.PlayPvs(drill.MiningSound, uid)?.Entity;
            if (!_groups.TryGetValue(vein, out var drills))
            {
                drills = _pool.Count > 0 ? _pool.Pop() : new List<EntityUid>();
                _groups.Add(vein, drills);
            }
            drills.Add(uid);
        }

        foreach (var (vein, drills) in _groups)
        {
            var mineral = _prototypes.Index(vein.Mineral);
            if (_prototypes.Index(mineral.Material).StackEntity is not { } ore)
            {
                foreach (var drill in drills)
                    StopAudio(Comp<OreDrillComponent>(drill));
                drills.Clear();
                _pool.Push(drills);
                continue;
            }
            var output = vein.Remainder + Math.Max(0f, vein.Rate) * elapsed;
            var count = Math.Min(vein.Remaining, (int)output);
            vein.Remainder = output - (int)output;
            vein.Remaining -= count;
            var share = count / drills.Count;
            var remainder = count % drills.Count;
            for (var i = 0; i < drills.Count; i++)
            {
                var index = (vein.NextDrill + i) % drills.Count;
                var amount = share + (i < remainder ? 1 : 0);
                if (amount > 0)
                    _spawn.SpawnCount(ore, Transform(drills[index]).Coordinates, amount);
            }
            vein.NextDrill = (vein.NextDrill + remainder) % drills.Count;
            drills.Clear();
            _pool.Push(drills);
        }
        _groups.Clear();
    }
}
