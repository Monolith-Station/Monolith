using System.Numerics;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem;

public abstract partial class SharedStarSystemMapSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _protoMan = default!;

    public PlanetarySystem? BuildPlanetarySystem(ProtoId<StarSystemPrototype> id)
    {
        if (!_protoMan.TryIndex(id, out var proto) ||
            !_protoMan.TryIndex(proto.Star, out var starProto))
            return null;

        var star = new Star(starProto, _protoMan);

        var planets = new List<Planet>();
        foreach (var entry in proto.Planets)
        {
            if (!_protoMan.TryIndex(entry.Planet, out var planetProto))
                continue;

            var position = new Vector2(entry.Distance, 0f);
            var parentIndex = planets.Count;
            planets.Add(new Planet(planetProto, _protoMan, entry.Planet, position));

            foreach (var moon in entry.Moons)
            {
                if (!_protoMan.TryIndex(moon.Moon, out var moonProto))
                    continue;

                planets.Add(new Planet(
                    moonProto,
                    _protoMan,
                    moon.Moon,
                    new Vector2(moon.Distance, 0f),
                    parentIndex));
            }
        }

        AsteroidBelt? belt = null;
        if (proto.AsteroidBelt is { } beltDef && _protoMan.TryIndex(beltDef.Type, out var beltProto))
            belt = new AsteroidBelt(beltDef, beltProto, star.Position);

        return new PlanetarySystem(star, planets, belt);
    }
}
