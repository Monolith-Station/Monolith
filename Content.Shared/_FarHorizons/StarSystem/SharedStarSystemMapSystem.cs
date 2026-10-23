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

            var position = new Vector2(MathF.Cos(entry.Angle), MathF.Sin(entry.Angle)) * entry.Distance;
            planets.Add(new Planet(planetProto, _protoMan, position));
        }

        AsteroidBelt? belt = null;
        if (proto.AsteroidBelt is { } beltDef && _protoMan.TryIndex(beltDef.Type, out var beltProto))
            belt = new AsteroidBelt(beltDef, beltProto, star.Position);

        return new PlanetarySystem(star, planets, belt);
    }
}
