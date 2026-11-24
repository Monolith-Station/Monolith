using System.Linq;
using System.Numerics;
using Content.Server._NF.Station.Systems;
using Content.Server._NF.Trade;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Station.Systems;
using Content.Server.Worldgen;
using Content.Server.Worldgen.Prototypes;
using Content.Server.Worldgen.Systems.Biomes;
using Content.Shared._NF.CCVar;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._NF.GameRule;

/// <summary>
/// Places the points of interest selected for a round.
/// </summary>
public sealed partial class PointOfInterestSystem : EntitySystem
{
    private const int PlacementPasses = 4;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MapLoaderSystem _map = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private StationRenameWarpsSystems _renameWarps = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private BiomeSelectionSystem _biomes = default!;

    private readonly Dictionary<MapId, List<PoiPlacement>> _poiPositions = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PostGameMapLoad>(OnGameMapLoaded);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _poiPositions.Clear();
    }

    private void OnGameMapLoaded(PostGameMapLoad ev)
    {
        if (ev.GameMap.IgnorePoiSeparation)
            _poiPositions.TryAdd(ev.Map, []);

        foreach (var grid in ev.Grids)
            _biomes.RegisterPoiSite(ev.Map, grid, CanSpawnInDroneZone(ev.GameMap.ID));
    }

    internal (
        List<EntityUid> Required,
        List<EntityUid> Depots,
        List<EntityUid> Markets,
        List<EntityUid> Optionals,
        List<EntityUid> Uniques) Generate(MapId mapId, ProtoId<GamePresetPrototype> preset)
    {
        var groups = _proto.EnumeratePrototypes<PointOfInterestPrototype>()
            .Where(proto => proto.CanSpawnIn(preset))
            .ToLookup(proto => proto.SpawnGroup);

        var required = new List<EntityUid>();
        var depots = new List<EntityUid>();
        var markets = new List<EntityUid>();
        var optionals = new List<EntityUid>();
        var uniques = new List<EntityUid>();
        var requests = new List<PoiPlacementRequest>();

        // Reserve narrow required POI bands, such as beacon slots, before the wider ones.
        foreach (var proto in groups["Required"].OrderBy(p => p.MaximumDistance).ThenByDescending(p => p.MinimumDistance))
            requests.Add(new(proto, required));

        var depotCount = _cfg.GetCVar(NFCCVars.CargoDepots);
        var depotPrototypes = groups["CargoDepot"].ToList();
        if (depotCount > 0 && depotPrototypes.Count > 0)
        {
            var sectorWidth = Math.Tau / depotCount;
            var rotationOffset = _random.NextAngle().Theta / depotCount;
            for (var index = 0; index < depotCount; index++)
            {
                var proto = _random.Pick(depotPrototypes);
                var depot = new DepotSlot(index, new Angle(rotationOffset + index * sectorWidth), sectorWidth / 2);
                requests.Add(new(proto, depots, depot));
            }
        }

        AddRandomSelection(groups["MarketStation"], _cfg.GetCVar(NFCCVars.MarketStations), markets, requests);
        AddRandomSelection(groups["Optional"], _cfg.GetCVar(NFCCVars.OptionalStations), optionals, requests);

        foreach (var group in groups)
        {
            if (group.Key is "Required" or "CargoDepot" or "MarketStation" or "Optional")
                continue;

            var alternatives = group.ToList();
            _random.Shuffle(alternatives);
            foreach (var proto in alternatives)
            {
                if (proto.SpawnChance <= 0 || _random.NextFloat() > proto.SpawnChance)
                    continue;

                requests.Add(new(proto, uniques));
                break;
            }
        }

        PlacePois(mapId, requests);
        return (required, depots, markets, optionals, uniques);
    }

    private void AddRandomSelection(
        IEnumerable<PointOfInterestPrototype> group,
        int count,
        List<EntityUid> destination,
        List<PoiPlacementRequest> requests)
    {
        if (count <= 0)
            return;

        var alternatives = group.ToList();
        _random.Shuffle(alternatives);
        foreach (var proto in alternatives.Take(count))
            requests.Add(new(proto, destination));
    }

    private void PlacePois(MapId mapId, List<PoiPlacementRequest> requests)
    {
        if (!_poiPositions.TryGetValue(mapId, out var positions))
        {
            // Reserve the origin for maps whose main station participates in POI separation.
            positions = [new(Vector2.Zero, 0)];
            _poiPositions.Add(mapId, positions);
        }

        var configuredDistance = _cfg.GetCVar(NFCCVars.MinPOIDistance);
        var minDistance = float.IsFinite(configuredDistance)
            ? Math.Max(0f, configuredDistance)
            : NFCCVars.MinPOIDistance.DefaultValue;
        var maxAttempts = Math.Max(0, _cfg.GetCVar(NFCCVars.POIPlacementRetries)) + 1L;
        var droneBiomes = _biomes.GetPoiExclusionBiomes(mapId, _cfg.GetCVar(CCVars.WorldgenConfig));

        // Give every selected POI a turn in its authored band. Retry the deferred sites together
        // with progressively more room. The final pass uses an unbounded search for ordinary POIs
        // and searches within the authored range for cargo depots.
        for (var pass = 0; pass < PlacementPasses && requests.Count > 0; pass++)
        {
            var deferred = new List<PoiPlacementRequest>();
            foreach (var request in requests)
            {
                if (TryPlacePoi(mapId, request, positions, minDistance, maxAttempts, pass, droneBiomes) == PlacementResult.Deferred)
                    deferred.Add(request);
            }

            requests = deferred;
        }
    }

    private PlacementResult TryPlacePoi(
        MapId mapId,
        PoiPlacementRequest request,
        List<PoiPlacement> positions,
        float globalMinDistance,
        long maxAttempts,
        int pass,
        IReadOnlyList<BiomePrototype> droneBiomes)
    {
        var proto = request.Prototype;
        var center = new Vector2(proto.PositionX, proto.PositionY);
        var outerPass = pass == PlacementPasses - 1;
        var minDistance = Math.Max(globalMinDistance, proto.MinimumSeparation);
        var expandedMax = request.Depot != null ? proto.MaximumDistance : proto.MaximumDistance + pass * minDistance;
        IReadOnlyList<BiomePrototype> avoidDroneBiomes = CanSpawnInDroneZone(proto.ID) ? [] : droneBiomes;
        for (long attempt = 0; attempt < (outerPass ? 1 : maxAttempts); attempt++)
        {
            var angle = outerPass ? new Angle(center) : _random.NextAngle();
            if (request.Depot is { } depot)
            {
                angle = depot.Angle;
                if (!outerPass && (pass > 0 || attempt > 0))
                    angle += _random.NextAngle(-depot.HalfSectorWidth, depot.HalfSectorWidth);
            }

            // Keep a small margin inside a depot's hard cap to absorb vector rounding.
            var upperRadius = Math.Max(proto.MinimumDistance, expandedMax - (request.Depot != null ? 0.01f : 0f));
            var position = !outerPass
                ? center + angle.ToVec() * _random.NextFloat(proto.MinimumDistance, upperRadius)
                : request.Depot != null
                    ? FindBoundedDepotPosition(center, positions, minDistance, proto.MinimumDistance, upperRadius, angle, avoidDroneBiomes)
                    : FindOuterPoiOutsideDroneZones(center, positions, minDistance, upperRadius, angle, avoidDroneBiomes);
            if (position is not { } candidate)
                break;

            if (IsInDroneZone(candidate, avoidDroneBiomes) ||
                !proto.IgnorePoiSeparation && !IsPositionClear(candidate, positions, minDistance))
                continue;

            var rotation = outerPass ? Angle.Zero : _random.NextAngle();
            if (!_map.TryLoadGrid(mapId, proto.GridPath, out var loadedGrid, offset: candidate, rot: rotation))
            {
                Log.Error($"Could not load POI {proto.ID} from {proto.GridPath}.");
                return PlacementResult.InvalidGrid;
            }

            EntityUid grid = loadedGrid.Value;
            var transform = Transform(grid);
            // The loader can add a saved root offset. Commit the checked position for the final
            // pass and keep depots inside their authored ranges before attaching station components.
            if ((outerPass || request.Depot != null) && transform.LocalPosition != candidate)
                _transform.SetCoordinates(grid, transform, new EntityCoordinates(transform.ParentUid, candidate));

            if (IsInDroneZone(transform.LocalPosition, avoidDroneBiomes) ||
                !proto.IgnorePoiSeparation && !IsPositionClear(transform.LocalPosition, positions, minDistance))
            {
                Del(grid);
                continue;
            }

            if (!proto.IgnorePoiSeparation)
                positions.Add(new(transform.LocalPosition, proto.MinimumSeparation));
            InitializePoi(grid, proto, request.Depot?.GetName(proto.Name));
            _biomes.RegisterPoiSite(mapId, grid, CanSpawnInDroneZone(proto.ID));
            if (request.Depot is { } depotSlot)
            {
                var station = _station.GetOwningStation(grid);
                if (TryComp<TradeCrateDestinationComponent>(station, out var destination))
                    destination.DestinationProto = depotSlot.Index < 26 ? $"Cargo{(char) ('A' + depotSlot.Index)}" : "CargoOther";
            }

            request.Destination.Add(grid);
            if (outerPass && request.Depot == null)
                Log.Info($"Placed deferred POI {proto.ID} at {transform.LocalPosition} in the outer search band.");

            return PlacementResult.Placed;
        }

        if (!outerPass)
            return PlacementResult.Deferred;

        var range = request.Depot == null ? "" : $" within its {proto.MinimumDistance}–{proto.MaximumDistance}m spawn band";
        Log.Error($"Could not find coordinates satisfying POI separation and drone-zone rules for {proto.ID} on map {mapId}{range}.");
        return PlacementResult.Unplaceable;
    }

    internal static bool CanSpawnInDroneZone(string poiId) => poiId is "HeliosFortress" or "HeliosFortressHyperwar" or "Underground";

    internal static bool IsInDroneZone(Vector2 position, IReadOnlyList<BiomePrototype> droneBiomes)
    {
        if (droneBiomes.Count == 0)
            return false;

        var chunk = WorldGen.WorldToChunkCoords(position).Floored();
        var lengthSquared = WorldGen.ChunkToWorldCoordsCentered(chunk).LengthSquared();
        foreach (var biome in droneBiomes)
        {
            if (biome.MatchesGeometry(chunk, lengthSquared))
                return true;
        }

        return false;
    }

    internal static Vector2? FindBoundedDepotPosition(
        Vector2 center,
        IReadOnlyList<PoiPlacement> positions,
        float minDistance,
        float minRadius,
        float maxRadius,
        Angle preferredAngle,
        IReadOnlyList<BiomePrototype>? droneBiomes = null)
    {
        const int bearingSamples = 96;
        const int radialSteps = 8;
        for (var radialStep = 0; radialStep <= radialSteps; radialStep++)
        {
            var radius = maxRadius - radialStep * (maxRadius - minRadius) / radialSteps;
            for (var bearing = 0; bearing < bearingSamples; bearing++)
            {
                var angle = preferredAngle + new Angle(Math.Tau * bearing / bearingSamples);
                var candidate = center + angle.ToVec() * radius;
                if (IsPositionClear(candidate, positions, minDistance) &&
                    (droneBiomes == null || !IsInDroneZone(candidate, droneBiomes)))
                    return candidate;
            }
        }

        return null;
    }

    internal static Vector2? FindOuterPoiOutsideDroneZones(
        Vector2 center,
        IReadOnlyList<PoiPlacement> positions,
        float minDistance,
        double minimumRadius,
        Angle preferredAngle,
        IReadOnlyList<BiomePrototype> droneBiomes)
    {
        const int bearingSamples = 96;
        for (var bearing = 0; bearing < bearingSamples; bearing++)
        {
            var angle = preferredAngle + new Angle(Math.Tau * bearing / bearingSamples);
            var candidate = GetOuterPoiPosition(center, positions, minDistance, minimumRadius, angle);
            if (!IsInDroneZone(candidate, droneBiomes))
                return candidate;
        }

        var outermostDroneRadius = droneBiomes.Max(biome => biome.GetOuterGeometryRadius());
        if (!double.IsFinite(outermostDroneRadius))
            return null;

        // Beyond this radius from the spawn center, no chunk can belong to a drone biome at any bearing.
        var clearRadius = outermostDroneRadius + center.Length();
        return GetOuterPoiPosition(center, positions, minDistance, Math.Max(minimumRadius, clearRadius), preferredAngle);
    }

    /// <summary>
    /// Finds a position past the occupied area at the chosen bearing from the band's center.
    /// </summary>
    internal static Vector2 GetOuterPoiPosition(
        Vector2 center,
        IReadOnlyList<PoiPlacement> positions,
        float minDistance,
        double minimumRadius,
        Angle angle)
    {
        var radius = minimumRadius;
        // For any existing point p, |candidate - p| >= radius - |p - center|.
        // Taking the maximum over all p makes this work at any bearing, for any placement order.
        foreach (var other in positions)
        {
            var x = (double) other.Position.X - center.X;
            var y = (double) other.Position.Y - center.Y;
            var pairDistance = Math.Max(minDistance, other.MinimumSeparation);
            radius = Math.Max(radius, Math.Sqrt(x * x + y * y) + pairDistance);
        }

        // Account for rounding when converting the radius and center to float coordinates.
        var scale = radius + Math.Max(Math.Abs((double) center.X), Math.Abs((double) center.Y));
        radius += Math.Max(1d, scale * 1e-6d);
        return new Vector2(
            (float) (center.X + Math.Cos(angle.Theta) * radius),
            (float) (center.Y + Math.Sin(angle.Theta) * radius));
    }

    private sealed record PoiPlacementRequest(PointOfInterestPrototype Prototype, List<EntityUid> Destination, DepotSlot? Depot = null);

    internal readonly record struct PoiPlacement(Vector2 Position, int MinimumSeparation);

    private enum PlacementResult
    {
        Placed,
        Deferred,
        InvalidGrid,
        Unplaceable,
    }

    private readonly record struct DepotSlot(int Index, Angle Angle, double HalfSectorWidth)
    {
        public string GetName(string name)
        {
            return Index < 26 ? $"{name} {(char) ('A' + Index)}" : $"{name} {Index + 1}";
        }
    }

    internal static bool IsPositionClear(Vector2 position, IReadOnlyList<PoiPlacement> placedPositions, float minDistance)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
            return false;

        foreach (var other in placedPositions)
        {
            var dx = (double) position.X - other.Position.X;
            var dy = (double) position.Y - other.Position.Y;
            var pairDistance = (double) Math.Max(minDistance, other.MinimumSeparation);
            if (dx * dx + dy * dy < pairDistance * pairDistance)
                return false;
        }

        return true;
    }

    private void InitializePoi(EntityUid gridUid, PointOfInterestPrototype proto, string? overrideName)
    {
        List<EntityUid> gridList = [gridUid];

        string stationName = string.IsNullOrEmpty(overrideName) ? proto.Name : overrideName;

        EntityUid? stationUid = null;
        if (_proto.TryIndex<GameMapPrototype>(proto.ID, out var stationProto))
            stationUid = _station.InitializeNewStation(stationProto.Stations[proto.ID], gridList, stationName);

        var meta = EnsureComp<MetaDataComponent>(gridUid);
        _meta.SetEntityName(gridUid, stationName, meta);

        EntityManager.AddComponents(gridUid, proto.AddComponents);

        // Rename warp points after set up if needed
        if (proto.NameWarp)
        {
            bool? hideWarp = proto.HideWarp ? true : null;
            if (stationUid != null)
                _renameWarps.SyncWarpPointsToStation(stationUid.Value, forceAdminOnly: hideWarp);
            else
                _renameWarps.SyncWarpPointsToGrids(gridList, forceAdminOnly: hideWarp);
        }
    }
}
