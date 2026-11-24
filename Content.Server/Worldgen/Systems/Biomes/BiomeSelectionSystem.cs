using System.Linq;
using System.Numerics;
using Content.Server.Worldgen.Components;
using Content.Server.Worldgen.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.Worldgen.Systems.Biomes;

/// <summary>
///     This handles biome selection, evaluating which biome to apply to a chunk based on noise channels.
/// </summary>
public sealed partial class BiomeSelectionSystem : BaseWorldSystem
{
    [Dependency] private NoiseIndexSystem _noiseIdx = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ISerializationManager _ser = default!;
    [Dependency] private IComponentFactory _components = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IRobustRandom _random = default!;

    internal BiomePrototype[] GetPoiExclusionBiomes(MapId map, string roundConfig)
    {
        // The round's POIs are generated before the worldgen configuration is applied.
        var biomes = _maps.TryGetMap(map, out var mapUid) && TryComp<BiomeSelectionComponent>(mapUid, out var selection)
            ? selection.Biomes.Select(id => _proto.Index<BiomePrototype>(id))
            : GetConfiguredBiomes(roundConfig);

        return biomes.Where(biome => biome.HostileDroneZone && !biome.AllowPoiClearings).ToArray();
    }

    internal void RegisterPoiSite(MapId map, EntityUid grid, bool allowsHostileDrones)
    {
        if (!_maps.TryGetMap(map, out var mapUid))
            return;

        var position = _transform.GetWorldPosition(grid);
        var bounds = _lookup.GetWorldAABB(grid);
        var extent = Vector2.Max(Vector2.Abs(bounds.BottomLeft - position), Vector2.Abs(bounds.TopRight - position));
        // Allow room outside the POI's grid bounds for arriving shuttles.
        EnsureComp<SectorPoiSitesComponent>(mapUid.Value).Sites.Add(new(position, extent.Length() + 384f, allowsHostileDrones));
    }

    internal IReadOnlyList<SectorPoiSite> GetPoiSites(EntityUid map)
    {
        return TryComp<SectorPoiSitesComponent>(map, out var sites) ? sites.Sites : [];
    }

    internal IReadOnlyList<SectorAsteroidBelt> GetAsteroidBelts(EntityUid map)
    {
        return TryComp<BiomeSelectionComponent>(map, out var selection) ? selection.AsteroidBelts : [];
    }

    internal BiomePrototype[] GetConfiguredBiomes(string configId)
    {
        if (!_proto.TryIndex<WorldgenConfigPrototype>(configId, out var config) ||
            !config.Components.TryGetValue(_components.GetComponentName<BiomeSelectionComponent>(), out var entry))
            return [];

        var selection = (BiomeSelectionComponent) entry.Component;
        return selection.Biomes.Select(id => _proto.Index<BiomePrototype>(id)).ToArray();
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<BiomeSelectionComponent, ComponentStartup>(OnBiomeSelectionStartup);
        SubscribeLocalEvent<BiomeSelectionComponent, WorldChunkAddedEvent>(OnWorldChunkAdded);
    }

    private void OnWorldChunkAdded(EntityUid uid, BiomeSelectionComponent component, ref WorldChunkAddedEvent args)
    {
        var biome = SelectBiome(uid, args.Coords, args.Chunk);
        if (biome != null)
            biome.Apply(args.Chunk, _ser, EntityManager);
        else
            Log.Error($"Biome selection ran out of biomes to select? See biomes list: {component.Biomes}");
    }

    internal BiomePrototype? SelectBiome(EntityUid map, Vector2i coords, EntityUid? noiseHolder = null)
    {
        if (!TryComp<BiomeSelectionComponent>(map, out var component))
            return null;

        if (component.RandomAsteroidBeltBiome is { } belt && component.AsteroidBeltChunks.Contains(coords))
            return _proto.Index(belt);

        var sites = GetPoiSites(map);
        var lengthSquared = WorldGen.ChunkToWorldCoordsCentered(coords).LengthSquared(); // Frontier: cache world coords of center of chunk

        foreach (var biomeId in component.Biomes)
        {
            var biome = _proto.Index<BiomePrototype>(biomeId);

            if (!biome.MatchesGeometry(coords, lengthSquared))
                continue;

            if (!biome.HasFixedChunkRange && !CheckNoiseRanges(noiseHolder ?? map, biome, coords))
                continue;

            if (biome.HostileDroneZone && IntersectsPoiClearance(coords, sites, hostileOnly: true))
                continue;

            return biome;
        }

        return null;
    }

    private void OnBiomeSelectionStartup(EntityUid uid, BiomeSelectionComponent component, ComponentStartup args)
    {
        var biomes = component.Biomes.Select(id => _proto.Index<BiomePrototype>(id)).OrderByDescending(biome => biome.Priority).ToArray();
        component.Biomes = biomes.Select(biome => biome.ID).ToList();

        // RoundStartingEvent applies the selector after the preset rules have loaded the POIs.
        if (component.RandomAsteroidBeltBiome is not { } beltId || _proto.Index(beltId) is not { DistanceRange: { } range } belt)
            return;

        var reservedBiomes = biomes.Where(biome => biome.Priority >= belt.Priority || biome.HostileDroneZone).ToArray();
        component.AsteroidBelts = GenerateAsteroidBelts(_random.Next(), range, reservedBiomes, GetPoiSites(uid));
        component.AsteroidBeltChunks = component.AsteroidBelts.SelectMany(region => region.Chunks).ToHashSet();
        if (component.AsteroidBelts.Count(region => region.Bounds.Bottom >= 0) < 6 ||
            component.AsteroidBelts.Count(region => region.Bounds.Top <= 0) < 6)
            Log.Warning("Could not fit six asteroid belts in each hemisphere while keeping POI clearances.");
    }

    private bool CheckNoiseRanges(EntityUid chunk, BiomePrototype biome, Vector2i coords)
    {
        foreach (var (noise, ranges) in biome.NoiseRanges)
        {
            var value = _noiseIdx.Evaluate(chunk, noise, coords);
            var anyValid = false;
            foreach (var range in ranges)
            {
                if (range.X < value && value < range.Y)
                {
                    anyValid = true;
                    break;
                }
            }

            if (!anyValid)
                return false;
        }
        return true;
    }
}
