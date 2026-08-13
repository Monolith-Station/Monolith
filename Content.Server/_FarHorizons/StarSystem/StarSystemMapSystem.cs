using Content.Server.GameTicking;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._FarHorizons.StarSystem;

public sealed partial class StarSystemMapSystem : SharedStarSystemMapSystem
{
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private PvsOverrideSystem _pvs = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PostGameMapLoad>(OnPostMapLoad);
    }

    private void OnPostMapLoad(PostGameMapLoad ev)
    {
        if (!_map.TryGetMap(ev.Map, out var mapUid)) return;
        var comp = EnsureComp<StarSystemMapComponent>(mapUid.Value);

        if (comp.System is { } system)
            SetSystem((mapUid.Value, comp), system);
    }

    public void SetSystem(Entity<StarSystemMapComponent> ent, ProtoId<StarSystemPrototype> system)
    {
        ent.Comp.System = system;
        ent.Comp.StarSystem = BuildPlanetarySystem(system);
        Dirty(ent);

        SpawnEntities(ent);
    }

    private void SpawnEntities(Entity<StarSystemMapComponent> ent)
    {
        if (ent.Comp.StarSystem == null)
            return;

        if (_protoMan.TryIndex<EntityPrototype>(Star.STAR_ENTITY, out var starEnt))
        {
            var coords = new EntityCoordinates(ent, ent.Comp.StarSystem.Star.Position);
            var spawned = SpawnAtPosition(starEnt.ID, coords);
            _metadata.SetEntityName(spawned, ent.Comp.StarSystem.Star.Name);
            _pvs.AddGlobalOverride(spawned);
        }

        if (_protoMan.TryIndex<EntityPrototype>(Planet.PLANET_ENTITY, out var planetEnt))
        {
            foreach (var planet in ent.Comp.StarSystem.Planets)
            {
                var planetCoords = new EntityCoordinates(ent, planet.Position);
                var spawnedPlanet = SpawnAtPosition(planetEnt.ID, planetCoords);
                _metadata.SetEntityName(spawnedPlanet, planet.Name);
                _pvs.AddGlobalOverride(spawnedPlanet);
            }
        }
    }
}
