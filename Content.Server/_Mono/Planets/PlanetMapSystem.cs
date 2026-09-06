using Content.Server._CE.ZLevels.Core;
using Content.Server.Parallax;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._Mono.Planets;
using Content.Shared.Light.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;

namespace Content.Server._Mono.Planets;

public sealed partial class PlanetMapSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private CEZLevelsSystem _levels = default!;
    [Dependency] private BiomeSystem _biomes = default!;
    [Dependency] private MetaDataSystem _metadata = default!;

    public EntityUid Create(ProtoId<PlanetMapPrototype> id, int? seed = null)
    {
        var definition = _prototypes.Index(id);
        if (definition.Layers.Count == 0)
            throw new ArgumentException($"Planet map {id} has no layers.");

        var network = _levels.CreateMapNetwork(definition.Components);
        var state = AddComp<PlanetMapGenerationComponent>(network);
        state.Definition = id;
        state.Seed = seed ?? _random.Next();
        _metadata.SetEntityName(network, definition.Name);

        try
        {
            EnsureComp<LightCycleComponent>(network);
            var maps = new Dictionary<EntityUid, int>();
            for (var i = 0; i < definition.Layers.Count; i++)
            {
                var depth = definition.BaseDepth + definition.Layers.Count - 1 - i;
                var map = CreateLayer(definition.Layers[i], state.Seed, false);
                state.Layers.Add(depth, map);
                maps.Add(map, depth);
                _metadata.SetEntityName(map, $"{definition.Name} [{depth}]");
            }

            if (!_levels.TryAddMapsIntoNetwork(network, maps))
                throw new InvalidOperationException($"Could not link planet map {id}.");

            _levels.InitializeZNetwork(network);
            return network;
        }
        catch
        {
            foreach (var map in state.Layers.Values)
                Del(map);
            Del(network);
            throw;
        }
    }

    /// <summary>
    /// Spawns a new PlanetMap layer.
    /// </summary>
    public EntityUid CreateLayer(PlanetMapLayer layer, int seed, bool initialize = false)
    {
        EntityUid? map = null;
        try
        {
            switch (layer)
            {
                case PlanetFileLayer file:
                    if (!_loader.TryLoadMap(file.Map, out var loaded, out _))
                        throw new InvalidOperationException($"Could not load planet layer {file.Map}.");
                    map = loaded.Value.Owner;
                    break;
                case PlanetBiomeLayer surface:
                    map = _maps.CreateMap(out _, runMapInit: false);
                    _biomes.EnsurePlanet(map.Value, _prototypes.Index(surface.Biome), unchecked(seed + surface.SeedOffset));
                    break;
                default:
                    throw new ArgumentException($"Unknown planet layer {layer.GetType().Name}.");
            }

            var zMap = EnsureComp<CEZMapComponent>(map.Value);
            zMap.ComponentOverrides = _serialization.CreateCopy(layer.Components, notNullableOverride: true);
            EntityManager.AddComponents(map.Value, zMap.ComponentOverrides);
            if (initialize)
                _maps.InitializeMap(map.Value);
            return map.Value;
        }
        catch
        {
            if (map is { } created)
                Del(created);
            throw;
        }
    }
}
