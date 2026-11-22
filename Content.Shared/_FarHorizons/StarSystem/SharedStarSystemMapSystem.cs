using System.Numerics;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.Shared._FarHorizons.StarSystem;

public abstract partial class SharedStarSystemMapSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IResourceManager _resources = default!;

    public PlanetarySystem? BuildPlanetarySystem(ProtoId<StarSystemPrototype> id)
    {
        if (!_protoMan.TryIndex(id, out var proto) ||
            !_protoMan.TryIndex(proto.Star, out var starProto))
            return null;

        var star = new Star(starProto, _protoMan);
        var projections = ReadShaderProjections();

        var planets = new List<Planet>();
        foreach (var entry in proto.Planets)
        {
            if (!_protoMan.TryIndex(entry.Planet, out var planetProto))
                continue;

            var position = new Vector2(entry.Distance, 0f);
            var parentIndex = planets.Count;
            planets.Add(new Planet(planetProto, _protoMan, entry.Planet, position,
                projections.GetValueOrDefault(planetProto.Shader, (0.05f, 5f))));

            foreach (var moon in entry.Moons)
            {
                if (!_protoMan.TryIndex(moon.Moon, out var moonProto))
                    continue;

                planets.Add(new Planet(
                    moonProto,
                    _protoMan,
                    moon.Moon,
                    new Vector2(moon.Distance, 0f),
                    projections.GetValueOrDefault(moonProto.Shader, (0.05f, 5f)),
                    parentIndex));
            }
        }

        AsteroidBelt? belt = null;
        if (proto.AsteroidBelt is { } beltDef && _protoMan.TryIndex(beltDef.Type, out var beltProto))
            belt = new AsteroidBelt(beltDef, beltProto, star.Position);

        return new PlanetarySystem(star, planets, belt);
    }

    private Dictionary<string, (float Parallax, float RadiusScale)> ReadShaderProjections()
    {
        // Shader prototypes are client-only, but the server also needs the orbital projection.
        var yaml = _resources.ContentFileReadYaml(new ResPath("/Prototypes/_FarHorizons/Shaders/shaders.yml"));
        var result = new Dictionary<string, (float, float)>();
        foreach (var node in (YamlSequenceNode) yaml.Documents[0].RootNode)
        {
            if (node is YamlMappingNode shader &&
                shader.TryGetNode("id", out var id) &&
                shader.TryGetNode("params", out var data) && data is YamlMappingNode parameters &&
                parameters.TryGetNode("parallaxFactor", out var parallax) &&
                parameters.TryGetNode("planetaryRadiusFactor", out var radius))
            {
                result[id.AsString()] = (parallax.AsFloat(), radius.AsFloat());
            }
        }

        return result;
    }
}
