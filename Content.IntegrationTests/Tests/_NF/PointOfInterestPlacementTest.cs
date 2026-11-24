using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server._NF.GameRule;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Server.Worldgen;
using Content.Server.Worldgen.Components;
using Content.Server.Worldgen.Prototypes;
using Content.Server.Worldgen.Systems.Biomes;
using Content.Shared.CCVar;
using Content.Shared._NF.CCVar;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.IntegrationTests.Tests._NF;

[TestFixture]
public sealed class PointOfInterestPlacementTest
{
    private const int DefaultSeparation = 750;
    private const int OutpostSeparation = 1200;
    private static readonly ProtoId<GameMapPrototype> Caelestinus = "Frontier";
    private static readonly ProtoId<GamePresetPrototype> ExemptPoiPreset = "PoiPlacementExemptTest";
    private static readonly ProtoId<PointOfInterestPrototype> StellarBlessing = "TSFMCIndustry";

    [TestPrototypes]
    private const string ExemptPoiPrototypes = """
        - type: gamePreset
          id: PoiPlacementExemptTest

        - type: pointOfInterest
          id: PoiPlacementExemptTestFirst
          name: First exempt site
          minimumDistance: 0
          maximumDistance: 0
          spawnGroup: Required
          spawnGamePreset: [PoiPlacementExemptTest]
          gridPath: /Maps/_Mono/POI/beaconstation_a.yml
          ignorePoiSeparation: true

        - type: pointOfInterest
          id: PoiPlacementExemptTestRegular
          name: Regular site
          minimumDistance: 0
          maximumDistance: 0
          spawnGroup: Optional
          spawnGamePreset: [PoiPlacementExemptTest]
          gridPath: /Maps/_Mono/POI/beaconstation_a.yml

        - type: pointOfInterest
          id: PoiPlacementExemptTestLast
          name: Last exempt site
          minimumDistance: 0
          maximumDistance: 0
          spawnGroup: PoiPlacementExemptTestGroup
          spawnGamePreset: [PoiPlacementExemptTest]
          gridPath: /Maps/_Mono/POI/beaconstation_a.yml
          ignorePoiSeparation: true

        - type: pointOfInterest
          id: PoiPlacementExemptTestDrone
          name: Site with a blocked drone zone position
          minimumDistance: 0
          maximumDistance: 0
          posX: 4224
          posY: -3392
          spawnGroup: Required
          spawnGamePreset: [PoiPlacementExemptTest]
          gridPath: /Maps/_Mono/POI/beaconstation_a.yml
          ignorePoiSeparation: true

        - type: pointOfInterest
          id: PoiPlacementExemptTestOuterPoi
          name: Ordinary site in an outer ring
          minimumDistance: 0
          maximumDistance: 0
          posY: 7000
          spawnGroup: Required
          spawnGamePreset: [PoiPlacementExemptTest]
          gridPath: /Maps/_Mono/POI/beaconstation_a.yml
        """;

    [Test]
    public async Task CaelestinusRemainsSelectableAtAnyPopulation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var maps = server.ResolveDependency<IGameMapManager>();

        await server.WaitAssertion(() =>
        {
            server.CfgMan.SetCVar(CCVars.GameMap, "");
            maps.SelectMapByConfigRules();
            var selected = maps.GetSelectedMap();
            Assert.That(selected?.ID, Is.EqualTo(Caelestinus.Id));
            Assert.That(selected?.MaxPlayers, Is.EqualTo(uint.MaxValue));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExemptPoiNeitherRequiresNorReservesSeparation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var central = prototypes.Index(Caelestinus);
            Assert.That(central.IgnorePoiSeparation, Is.True);

            var grids = entMan.System<GameTicker>().LoadGameMap(central, out var mapId);
            Assert.That(entMan.GetComponent<TransformComponent>(grids.Single()).LocalPosition, Is.EqualTo(Vector2.Zero));

            var pois = entMan.System<PointOfInterestSystem>().Generate(mapId, ExemptPoiPreset);
            var first = pois.Required.Single(grid => entMan.GetComponent<MetaDataComponent>(grid).EntityName == "First exempt site");
            Assert.That(entMan.GetComponent<TransformComponent>(first).LocalPosition, Is.EqualTo(Vector2.Zero));
            Assert.That(entMan.GetComponent<TransformComponent>(pois.Optionals.Single()).LocalPosition, Is.EqualTo(Vector2.Zero));
            Assert.That(entMan.GetComponent<TransformComponent>(pois.Uniques.Single()).LocalPosition, Is.EqualTo(Vector2.Zero));

            var biomes = entMan.System<BiomeSelectionSystem>();
            var droneBiomes = biomes.GetPoiExclusionBiomes(mapId, "MonoDefault");
            Assert.That(PointOfInterestSystem.IsInDroneZone(new Vector2(4224, -3392), droneBiomes), Is.True);
            var displaced = pois.Required.Single(grid => entMan.GetComponent<MetaDataComponent>(grid).EntityName == "Site with a blocked drone zone position");
            Assert.That(PointOfInterestSystem.IsInDroneZone(entMan.GetComponent<TransformComponent>(displaced).LocalPosition, droneBiomes), Is.False);

            var ringSite = pois.Required.Single(grid => entMan.GetComponent<MetaDataComponent>(grid).EntityName == "Ordinary site in an outer ring");
            var ringPosition = entMan.GetComponent<TransformComponent>(ringSite).LocalPosition;
            Assert.That(Vector2.DistanceSquared(ringPosition, new Vector2(0, 7000)), Is.LessThan(128 * 128));
            Assert.That(entMan.System<SharedMapSystem>().TryGetMap(mapId, out var map), Is.True);
            Assert.That(biomes.GetAsteroidBelts(map!.Value), Is.Empty);
            prototypes.Index(new ProtoId<WorldgenConfigPrototype>("MonoDefault"))
                .Apply(map.Value, server.ResolveDependency<ISerializationManager>(), entMan);
            var siteChunk = WorldGen.WorldToChunkCoords(ringPosition).Floored();
            Assert.That(biomes.SelectBiome(map.Value, siteChunk)?.HostileDroneZone, Is.False);
            Assert.That(biomes.SelectBiome(map.Value, new Vector2i(54, 0))?.HostileDroneZone, Is.True);
            Assert.That(biomes.GetAsteroidBelts(map.Value), Is.Not.Empty);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DefaultPresetPoiCountsFitInTheirSpawnBands()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.That(NFCCVars.MinPOIDistance.DefaultValue, Is.EqualTo(DefaultSeparation));
            var droneBiomes = server.EntMan.System<BiomeSelectionSystem>()
                .GetConfiguredBiomes("MonoDefault").Where(biome => biome.HostileDroneZone && !biome.AllowPoiClearings).ToArray();
            var pois = prototypes.EnumeratePrototypes<PointOfInterestPrototype>().OrderBy(p => p.ID).ToArray();
            foreach (var id in new[] { "HeliosFortress", "HeliosFortressHyperwar", "Underground" })
                Assert.That(pois.Single(p => p.ID == id).MinimumSeparation, Is.EqualTo(OutpostSeparation), id);

            var presets = prototypes.EnumeratePrototypes<GamePresetPrototype>()
                .Where(p => p.Rules.Contains("NFAdventure") && !p.Rules.Contains("Hyperwar"))
                .ToArray();
            Assert.That(presets, Is.Not.Empty);

            foreach (var preset in presets)
            {
                var groups = pois.Where(p => p.CanSpawnIn(preset.ID)).ToLookup(p => p.SpawnGroup);
                var rotations = Math.Max(1, groups.Where(g => g.Key != "Required").Select(g => g.Count()).DefaultIfEmpty().Max());
                for (var rotation = 0; rotation < rotations; rotation++)
                {
                    var bands = SelectBands(groups, rotation);
                    Assert.That(TryFindLayout(bands, DefaultSeparation, out var positions, droneBiomes), Is.True,
                        $"{preset.ID}: no {DefaultSeparation}m layout found on the test lattice for " +
                        $"{bands.Count} POIs in their authored bands: {string.Join(", ", bands.Select(band => band.Id))}");

                    for (var i = 0; i < positions.Length; i++)
                    {
                        Assert.That(bands[i].Contains(positions[i]), Is.True, $"{preset.ID}: {bands[i].Id} outside its band.");
                        if (!PointOfInterestSystem.CanSpawnInDroneZone(bands[i].Id))
                            Assert.That(PointOfInterestSystem.IsInDroneZone(positions[i], droneBiomes), Is.False, bands[i].Id);
                        var originDistance = Math.Max(DefaultSeparation, bands[i].MinimumSeparation);
                        Assert.That(positions[i].LengthSquared(), Is.AtLeast(originDistance * originDistance));
                        for (var j = 0; j < i; j++)
                        {
                            var pairDistance = Math.Max(originDistance, bands[j].MinimumSeparation);
                            Assert.That(Vector2.DistanceSquared(positions[i], positions[j]),
                                Is.AtLeast(pairDistance * pairDistance), $"{preset.ID}: {bands[i].Id} and {bands[j].Id}.");
                        }
                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MonoSectorBeltsAndDroneZonesUseTheConfiguredGeometry()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var biomes = server.EntMan.System<BiomeSelectionSystem>().GetConfiguredBiomes("MonoDefault");
            var droneBiomes = biomes.Where(biome => biome.HostileDroneZone).ToArray();
            foreach (var biome in biomes)
            {
                Assert.That(prototypes.TryGetMapping(typeof(BiomePrototype), biome.ID, out var data), Is.True);
                var hasDroneSpawner = data!.TryGet<SequenceDataNode>("chunkComponents", out var components) &&
                     components.OfType<MappingDataNode>().Any(component =>
                         component.TryGet<SequenceDataNode>("debrisTable", out var table) &&
                         table.OfType<MappingDataNode>().Any(entry =>
                             entry.TryGet<ValueDataNode>("id", out var id) &&
                             id.Value.StartsWith("MonoDroneSpawnerT", StringComparison.Ordinal)));
                Assert.That(biome.HostileDroneZone, Is.EqualTo(hasDroneSpawner), biome.ID);
            }

            var rings = droneBiomes.Where(biome => biome.AllowPoiClearings).ToArray();
            Assert.That(rings.Select(ring => ring.DistanceRange), Is.EqualTo(new Vector2?[]
            {
                new(4500, 5500), new(5500, 6250), new(6250, 7500), new(7500, 10000),
            }));
            Assert.That(rings.All(ring => ring.ChunkRegions.Count == 0 && !ring.HasFixedChunkRange), Is.True);

            var exceptions = prototypes.EnumeratePrototypes<PointOfInterestPrototype>()
                .Where(poi => PointOfInterestSystem.CanSpawnInDroneZone(poi.ID)).Select(poi => poi.ID);
            Assert.That(exceptions, Is.EquivalentTo(new[] { "HeliosFortress", "HeliosFortressHyperwar", "Underground" }));
            var stellar = prototypes.Index(StellarBlessing);
            Assert.That(PointOfInterestSystem.IsInDroneZone(new Vector2(stellar.PositionX, stellar.PositionY), rings), Is.True);

            var blocked = new Vector2(0, 7000);
            Assert.That(PointOfInterestSystem.IsInDroneZone(blocked, droneBiomes), Is.True);
            var fallback = PointOfInterestSystem.FindOuterPoiOutsideDroneZones(blocked, [], DefaultSeparation, 0, Angle.Zero, droneBiomes);
            Assert.That(fallback.HasValue, Is.True);
            Assert.That(PointOfInterestSystem.IsInDroneZone(fallback!.Value, droneBiomes), Is.False);
            Assert.That(fallback.Value.Length(), Is.GreaterThan(droneBiomes.Max(biome => biome.GetOuterGeometryRadius())));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MonoInnerBeltsVaryBySeedAndFitBetweenOccupiedSites()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var inner = prototypes.Index(new ProtoId<BiomePrototype>("MonoInnerAsteroidBelts"));
            var reserved = server.EntMan.System<BiomeSelectionSystem>().GetConfiguredBiomes("MonoDefault")
                .Where(biome => biome.Priority >= inner.Priority || biome.HostileDroneZone).ToArray();
            SectorPoiSite[] sites = [new(new(1000, 1200), 500, false), new(new(-2300, -1900), 550, false)];
            var previous = Array.Empty<Box2i>();
            for (var seed = 0; seed < 32; seed++)
            {
                var belts = BiomeSelectionSystem.GenerateAsteroidBelts(seed, inner.DistanceRange!.Value, reserved, sites);
                Assert.That(belts.Count(belt => belt.Bounds.Bottom >= 0), Is.InRange(6, 8));
                Assert.That(belts.Count(belt => belt.Bounds.Top <= 0), Is.InRange(6, 8));
                Assert.That(belts.Any(belt => belt.Bounds.Area > belt.Chunks.Count), Is.True);
                var bounds = belts.Select(belt => belt.Bounds).ToArray();
                Assert.That(bounds, Is.Not.EqualTo(previous));
                previous = bounds;
                var sharedChunks = new HashSet<Vector2i>();
                foreach (var belt in belts)
                {
                    Assert.That(Math.Max(belt.Bounds.Width, belt.Bounds.Height) * WorldGen.ChunkSize, Is.AtMost(800));
                    foreach (var chunk in belt.Chunks)
                    {
                        Assert.That(sharedChunks.Add(chunk), Is.True);
                        Assert.That(belt.Bounds.ContainsTile(chunk), Is.True);
                        Assert.That(BiomeSelectionSystem.IntersectsPoiClearance(chunk, sites, hostileOnly: false), Is.False);
                        var lengthSquared = WorldGen.ChunkToWorldCoordsCentered(chunk).LengthSquared();
                        Assert.That(reserved.Any(biome => biome.MatchesGeometry(chunk, lengthSquared)), Is.False);
                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(500, 0, 0)]
    [TestCase(1000, OutpostSeparation, 0)]
    [TestCase(1000, 0, OutpostSeparation)]
    public void TwoFixedSitesCloserThanTheMinimumCannotFit(int distance, int firstMinimum, int secondMinimum)
    {
        var firstPosition = new Vector2(1500, 0);
        var secondPosition = new Vector2(1500 + distance, 0);
        List<SpawnBand> bands =
        [
            new("First", firstPosition, 0, 0, firstMinimum, null, 0),
            new("Second", secondPosition, 0, 0, secondMinimum, null, 0),
        ];

        Assert.That(TryFindLayout(bands, DefaultSeparation, out _), Is.False);
        Assert.That(PointOfInterestSystem.IsPositionClear(secondPosition, [new(firstPosition, firstMinimum)],
            Math.Max(DefaultSeparation, secondMinimum)), Is.False);
    }

    [Test]
    public void DepotFallbackFindsAnInRangePositionWhenItsBearingIsBlocked()
    {
        const int minimumRadius = 3000;
        const int maximumRadius = 4000;
        List<PointOfInterestSystem.PoiPlacement> occupied = [new(new Vector2(3500, 0), OutpostSeparation)];

        var result = PointOfInterestSystem.FindBoundedDepotPosition(
            Vector2.Zero, occupied, DefaultSeparation, minimumRadius, maximumRadius - 0.01f, Angle.Zero);

        Assert.That(result.HasValue, Is.True);
        Assert.That(result.Value.Length(), Is.InRange(minimumRadius, maximumRadius));
        Assert.That(PointOfInterestSystem.IsPositionClear(result.Value, occupied, DefaultSeparation), Is.True);
    }

    [Test]
    public void OuterPassFitsDeferredSitesRegardlessOfBearing()
    {
        Vector2[] blockedCenters = [new(5000, -6000), new(-2300, 1100), new(1000, 3000)];
        List<PointOfInterestSystem.PoiPlacement> occupied = [new(Vector2.Zero, 0)];
        occupied.AddRange(blockedCenters.Select(center => new PointOfInterestSystem.PoiPlacement(center, OutpostSeparation)));
        const int maximumAuthoredRadius = 200;
        for (var i = 0; i < 48; i++)
        {
            // Each site starts out in a narrow band that is entirely blocked by another POI.
            var center = blockedCenters[i % blockedCenters.Length];
            var angle = new Angle(i * Math.Tau / 48);
            var minimumSeparation = i % 3 == 0 ? OutpostSeparation : DefaultSeparation;
            var position = PointOfInterestSystem.GetOuterPoiPosition(
                center, occupied, minimumSeparation, maximumAuthoredRadius, angle);

            Assert.That(Vector2.DistanceSquared(center, position), Is.GreaterThan(maximumAuthoredRadius * maximumAuthoredRadius));
            foreach (var other in occupied)
            {
                var pairDistance = Math.Max(minimumSeparation, other.MinimumSeparation);
                Assert.That(Vector2.DistanceSquared(position, other.Position), Is.AtLeast(pairDistance * pairDistance));
            }

            occupied.Add(new(position, minimumSeparation));
        }
    }

    private static List<SpawnBand> SelectBands(ILookup<string, PointOfInterestPrototype> groups, int rotation)
    {
        var bands = groups["Required"].Select(p => SpawnBand.FromPrototype(p)).ToList();
        var depotCount = NFCCVars.CargoDepots.DefaultValue;
        var depots = groups["CargoDepot"].ToArray();
        if (depotCount > 0 && depots.Length > 0)
        {
            var sectorWidth = Math.Tau / depotCount;
            for (var i = 0; i < depotCount; i++)
            {
                var depot = depots[(rotation + i) % depots.Length];
                Assert.That(depot.MaximumDistance, Is.AtMost(4000), depot.ID);
                bands.Add(SpawnBand.FromPrototype(depot, i * sectorWidth, sectorWidth / 2));
            }
        }

        AddRotatedChoices(groups["MarketStation"], NFCCVars.MarketStations.DefaultValue);
        AddRotatedChoices(groups["Optional"], NFCCVars.OptionalStations.DefaultValue);
        foreach (var group in groups)
        {
            if (group.Key is "Required" or "CargoDepot" or "MarketStation" or "Optional")
                continue;

            AddRotatedChoices(group.Where(p => p.SpawnChance > 0), 1);
        }

        return bands;

        void AddRotatedChoices(IEnumerable<PointOfInterestPrototype> choices, int count)
        {
            var available = choices.ToArray();
            for (var i = 0; i < Math.Min(count, available.Length); i++)
                bands.Add(SpawnBand.FromPrototype(available[(rotation + i) % available.Length]));
        }
    }

    private static bool TryFindLayout(List<SpawnBand> bands, int separation, out Vector2[] positions,
        IReadOnlyList<BiomePrototype> droneBiomes = null)
    {
        positions = new Vector2[bands.Count];
        var layout = positions;
        var fixedPositions = new List<(Vector2 Position, int MinimumSeparation)> { (Vector2.Zero, 0) };
        var flexible = new List<int>();
        var largeZones = new List<int>();
        for (var i = 0; i < bands.Count; i++)
        {
            var band = bands[i];
            if (band.MinimumRadius != 0 || band.MaximumRadius >= separation / 2f || band.SectorAngle != null)
            {
                (band.MinimumSeparation > separation ? largeZones : flexible).Add(i);
                continue;
            }

            // Use the center as a witness for a beacon or other POI with a tight, authored offset.
            if (!IsClear(band.Center, band))
                return false;

            positions[i] = band.Center;
            fixedPositions.Add((band.Center, band.MinimumSeparation));
        }

        return PlaceLargeZones(0);

        bool PlaceLargeZones(int slot)
        {
            if (slot == largeZones.Count)
                return MatchRemainingBands();

            var index = largeZones[slot];
            var band = bands[index];
            foreach (var position in GetCandidates(band))
            {
                layout[index] = position;
                fixedPositions.Add((position, band.MinimumSeparation));
                if (PlaceLargeZones(slot + 1))
                    return true;

                fixedPositions.RemoveAt(fixedPositions.Count - 1);
            }

            return false;
        }

        bool MatchRemainingBands()
        {
            // After reserving the larger exclusion zones, distinct lattice points are far enough apart.
            // A complete bipartite matching provides a layout even when several bands overlap.
            var ownerByPosition = new Dictionary<Vector2, int>();
            var candidates = flexible.ToDictionary(i => i, i => GetCandidates(bands[i]).ToArray());
            foreach (var index in flexible.OrderBy(i => candidates[i].Length))
            {
                if (!Assign(index, []))
                    return false;
            }

            foreach (var (position, owner) in ownerByPosition)
                layout[owner] = position;

            return true;

            bool Assign(int index, HashSet<Vector2> tried)
            {
                foreach (var position in candidates[index])
                {
                    if (!tried.Add(position))
                        continue;

                    if (ownerByPosition.TryGetValue(position, out var owner) && !Assign(owner, tried))
                        continue;

                    ownerByPosition[position] = index;
                    return true;
                }

                return false;
            }
        }

        IEnumerable<Vector2> GetCandidates(SpawnBand band)
        {
            var extent = new Vector2(band.MaximumRadius);
            var min = (band.Center - extent) / separation;
            var max = (band.Center + extent) / separation;
            for (var x = (int) MathF.Ceiling(min.X); x <= (int) MathF.Floor(max.X); x++)
            {
                for (var y = (int) MathF.Ceiling(min.Y); y <= (int) MathF.Floor(max.Y); y++)
                {
                    var position = new Vector2(x, y) * separation;
                    if (band.Contains(position) && IsClear(position, band))
                        yield return position;
                }
            }
        }

        bool IsClear(Vector2 position, SpawnBand band)
        {
            if (droneBiomes != null && !PointOfInterestSystem.CanSpawnInDroneZone(band.Id) &&
                PointOfInterestSystem.IsInDroneZone(position, droneBiomes))
                return false;

            foreach (var other in fixedPositions)
            {
                var pairDistance = Math.Max(separation, Math.Max(band.MinimumSeparation, other.MinimumSeparation));
                if (Vector2.DistanceSquared(position, other.Position) < pairDistance * pairDistance)
                    return false;
            }

            return true;
        }
    }

    private readonly record struct SpawnBand(
        string Id,
        Vector2 Center,
        int MinimumRadius,
        int MaximumRadius,
        int MinimumSeparation,
        double? SectorAngle,
        double SectorHalfWidth)
    {
        public static SpawnBand FromPrototype(PointOfInterestPrototype poi, double? angle = null, double halfWidth = 0)
        {
            return new(poi.ID, new Vector2(poi.PositionX, poi.PositionY), poi.MinimumDistance, poi.MaximumDistance,
                poi.MinimumSeparation, angle, halfWidth);
        }

        public bool Contains(Vector2 point)
        {
            var offset = point - Center;
            var radius = offset.Length();
            return radius >= MinimumRadius && radius <= MaximumRadius &&
                (SectorAngle is not { } angle || Math.Abs(Math.IEEERemainder(Math.Atan2(offset.Y, offset.X) - angle, Math.Tau)) <= SectorHalfWidth);
        }
    }
}
