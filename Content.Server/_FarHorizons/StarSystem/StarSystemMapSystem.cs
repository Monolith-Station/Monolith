using System.Numerics;
using Content.Server.GameTicking;
using Content.Server._CE.ZLevels.Core;
using Content.Server._Mono.Planets;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._FarHorizons.StarSystem;

public sealed partial class StarSystemMapSystem : SharedStarSystemMapSystem
{
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private PvsOverrideSystem _pvs = default!;
    [Dependency] private PlanetMapSystem _planetMaps = default!;
    [Dependency] private CEZLevelsSystem _zLevels = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PostGameMapLoad>(OnPostMapLoad);
        SubscribeLocalEvent<PlanetBodyComponent, ComponentShutdown>(OnPlanetShutdown);
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

        EnsureComp<StarLightComponent>(ent);

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
            var spawnedBodies = new List<EntityUid>();
            for (var i = 0; i < ent.Comp.StarSystem.Planets.Count; i++)
            {
                var planet = ent.Comp.StarSystem.Planets[i];
                var center = planet.ParentIndex is { } parent
                    ? Transform(spawnedBodies[parent]).LocalPosition
                    : Vector2.Zero;
                var position = center + _random.NextAngle().ToVec() * planet.Position.Length();
                var planetCoords = new EntityCoordinates(ent, position);
                var spawnedPlanet = SpawnAtPosition(planetEnt.ID, planetCoords);
                spawnedBodies.Add(spawnedPlanet);
                _metadata.SetEntityName(spawnedPlanet, planet.Name);

                var body = EnsureComp<PlanetBodyComponent>(spawnedPlanet);
                body.StarSystemMap = ent;
                body.Type = planet.Type;
                body.Index = i;
                body.Radius = planet.Radius;
                body.BodyScale = planet.BodyScale;
                body.ParentBody = planet.ParentIndex is { } parentIndex ? spawnedBodies[parentIndex] : null;

                var planetProto = _protoMan.Index(planet.Type);
                if (planetProto.Surface is { } surface)
                {
                    var network = _planetMaps.Create(surface);
                    _metadata.SetEntityName(network, planet.Name);
                    body.SurfaceNetwork = network;
                    var surfaceComp = EnsureComp<PlanetSurfaceComponent>(network);
                    surfaceComp.Planet = spawnedPlanet;
                    surfaceComp.SpaceMap = ent;
                    Dirty(network, surfaceComp);
                    _pvs.AddGlobalOverride(network);
                }

                Dirty(spawnedPlanet, body);
                _pvs.AddGlobalOverride(spawnedPlanet);
            }
        }
    }

    private void OnPlanetShutdown(Entity<PlanetBodyComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.SurfaceNetwork is { } network && !TerminatingOrDeleted(network))
            _zLevels.DeleteMapNetwork(network);
    }
}
