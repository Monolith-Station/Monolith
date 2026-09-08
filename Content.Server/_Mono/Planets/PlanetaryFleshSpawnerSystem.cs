using System.Linq;
using System.Numerics;
using Content.Server.Popups;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Planets;

public sealed partial class PlanetaryFleshSpawnerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private MobStateSystem _mobs = default!;
    [Dependency] private PopupSystem _popup = default!;
    private TimeSpan _nextUpdate;

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate)
            return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
        var maps = EntityQueryEnumerator<PlanetaryFleshSpawnerComponent>();
        while (maps.MoveNext(out var map, out var spawner))
        {
            foreach (var (uid, expires) in spawner.Spawned.ToArray())
            {
                if (!TerminatingOrDeleted(uid) && _timing.CurTime < expires)
                    continue;
                if (!TerminatingOrDeleted(uid))
                {
                    _popup.PopupEntity(Loc.GetString("aerumna-flesh-reclaimed"), uid);
                    QueueDel(uid);
                }
                spawner.Spawned.Remove(uid);
            }

            if (_timing.CurTime < spawner.NextSpawn || spawner.Mobs.Count == 0)
                continue;
            spawner.NextSpawn = _timing.CurTime + spawner.Interval;
            var players = EntityQueryEnumerator<ActorComponent, TransformComponent>();
            while (players.MoveNext(out var player, out var actor, out var xform))
            {
                if (spawner.Spawned.Count >= spawner.MapLimit)
                    break;
                if (xform.MapUid != map || actor.PlayerSession.Status != SessionStatus.InGame
                    || actor.PlayerSession.AttachedEntity != player || !_mobs.IsAlive(player)
                    || _containers.IsEntityInContainer(player))
                    continue;
                var center = _transform.GetWorldPosition(xform);
                var nearby = 0;
                foreach (var mob in spawner.Spawned.Keys)
                {
                    if (Transform(mob).MapUid == map
                        && Vector2.DistanceSquared(center, _transform.GetWorldPosition(mob)) < spawner.Radius * spawner.Radius)
                        nearby++;
                }
                if (nearby >= spawner.NearbyLimit)
                    continue;

                for (var attempt = 0; attempt < 12; attempt++)
                {
                    var angle = _random.NextFloat() * MathF.Tau;
                    var position = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle))
                        * (6f + _random.NextFloat() * 6f);
                    var coordinates = new EntityCoordinates(map, position);
                    if (!_turf.TryGetTileRef(coordinates, out var tile)
                        || _turf.GetContentTileDefinition(tile.Value).ID != "FloorAerumnaFlesh")
                        continue;
                    var mob = Spawn(_random.Pick(spawner.Mobs), coordinates);
                    spawner.Spawned.Add(mob, _timing.CurTime + spawner.Lifetime);
                    _popup.PopupEntity(Loc.GetString("aerumna-flesh-emerges"), mob);
                    break;
                }
            }
        }
    }
}
