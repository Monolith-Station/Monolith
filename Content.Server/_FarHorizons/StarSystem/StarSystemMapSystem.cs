using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._FarHorizons.StarSystem;

public sealed partial class StarSystemMapSystem : SharedStarSystemMapSystem
{

    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IRobustRandom _rand = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PostGameMapLoad>(OnPostMapLoad);
    }

    private void OnPostMapLoad(PostGameMapLoad ev)
    {
        if (!_map.TryGetMap(ev.Map, out var mapUid)) return;
        var comp = EnsureComp<StarSystemMapComponent>(mapUid.Value);
        SetSeed((mapUid.Value, comp), _ticker.RoundId, false);

        EntityUid? station = null;
        foreach (var grid in ev.Grids)
        {
            if (!HasComp<BecomesStationComponent>(grid))
                continue;
            
            station = grid;
            break;
        }

        if (station == null) return;

        var prettyPlanets = GetPrettyPlanets((mapUid.Value, comp));

        if (!prettyPlanets.Any()) return;

        var orbitPos = prettyPlanets.First().GetPointOnOrbit(_rand);
        var stationPos = _transform.GetMapCoordinates(station.Value).Position;
        var delta = stationPos - orbitPos;

        comp.StarOffset = delta;
        Dirty<StarSystemMapComponent>((mapUid.Value, comp));

        SpawnEntities((mapUid.Value, comp));
    }

    public void SetSeed(Entity<StarSystemMapComponent> ent, int seed, bool sync = true)
    {
        ent.Comp.Seed = HashSeed(seed);
        if (sync)
            Dirty(ent);
        ent.Comp.StarSystem = MakePlanetarySystem(ent);
    }

    // Shuffle bits around to create entropy
    private static int HashSeed(int input)
    {
        var x = (uint)input;
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        
        return (int)x; 
    }

    private void SpawnEntities(Entity<StarSystemMapComponent> ent)
    {
        if (ent.Comp.StarSystem == null)
            return;

        if (_protoMan.TryIndex<EntityPrototype>(Star.STAR_ENTITY, out var starEnt))
        {
            var coords = new EntityCoordinates(ent, ent.Comp.StarSystem.Star.Position + ent.Comp.StarOffset);
            var spawned = SpawnAtPosition(starEnt.ID, coords);
            var name = Loc.GetString("space-star-warp-name", ("star", ent.Comp.StarSystem.Star.Name));
            _metadata.SetEntityName(spawned, name);
        }

        if (_protoMan.TryIndex<EntityPrototype>(Planet.PLANET_ENTITY, out var planetEnt))
        {
            foreach (var planet in ent.Comp.StarSystem.Planets)
            {
                var planetCoords = new EntityCoordinates(ent, planet.Position + ent.Comp.StarOffset);
                var spawnedPlanet = SpawnAtPosition(planetEnt.ID, planetCoords);
                var name = Loc.GetString("space-planet-warp-name", ("planet", planet.Name));
                _metadata.SetEntityName(spawnedPlanet, name);
            }
        }
    }
}