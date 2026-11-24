using System.Numerics;
using Content.Server.Worldgen.Components;
using Content.Server.Worldgen.Prototypes;

namespace Content.Server.Worldgen.Systems.Biomes;

public sealed partial class BiomeSelectionSystem
{
    // Six chunks give a 768 m bounding box, below the 800 m limit.
    private const int MaximumBeltSide = 800 / WorldGen.ChunkSize;

    internal static List<SectorAsteroidBelt> GenerateAsteroidBelts(
        int seed,
        Vector2 range,
        IReadOnlyList<BiomePrototype> reservedBiomes,
        IReadOnlyList<SectorPoiSite> sites)
    {
        var random = new Random(seed);
        var limit = (int) MathF.Ceiling(range.Y / WorldGen.ChunkSize);
        var eligible = new HashSet<Vector2i>();
        var north = new List<Vector2i>();
        var south = new List<Vector2i>();
        var regions = new List<SectorAsteroidBelt>(16);

        for (var y = -limit; y < limit; y++)
        for (var x = -limit; x < limit; x++)
        {
            var chunk = new Vector2i(x, y);
            if (!IsEligible(chunk))
                continue;

            eligible.Add(chunk);
            (y < 0 ? south : north).Add(chunk);
        }

        GenerateHemisphere(north.ToArray(), north: true);
        GenerateHemisphere(south.ToArray(), north: false);
        return regions;

        bool IsEligible(Vector2i chunk)
        {
            var min = WorldGen.ChunkToWorldCoords(chunk);
            var max = min + new Vector2(WorldGen.ChunkSize);
            if (Vector2.Clamp(Vector2.Zero, min, max).LengthSquared() < range.X * range.X ||
                Vector2.Max(Vector2.Abs(min), Vector2.Abs(max)).LengthSquared() >= range.Y * range.Y ||
                IntersectsPoiClearance(chunk, sites, hostileOnly: false))
                return false;

            var radiusSquared = WorldGen.ChunkToWorldCoordsCentered(chunk).LengthSquared();
            foreach (var biome in reservedBiomes)
            {
                if (biome.MatchesGeometry(chunk, radiusSquared))
                    return false;
            }

            return true;
        }

        void GenerateHemisphere(Vector2i[] origins, bool north)
        {
            random.Shuffle(origins.AsSpan());
            var remaining = random.Next(6, 9);
            // Prefer full-sized shapes. Scan the same shuffled positions at smaller sizes if a busy
            // map leaves little space between POIs.
            for (var maxSide = MaximumBeltSide; maxSide >= 2 && remaining > 0; maxSide--)
            {
                foreach (var origin in origins)
                {
                    var width = random.Next(Math.Min(3, maxSide), maxSide + 1);
                    var height = random.Next(Math.Min(3, maxSide), maxSide + 1);
                    var bounds = Box2i.FromDimensions(origin.X, origin.Y, width, height);
                    if (!north && bounds.Top > 0 || !CanPlace(bounds))
                        continue;

                    var chunks = new List<Vector2i>(bounds.Area);
                    for (var y = bounds.Bottom; y < bounds.Top; y++)
                    {
                        // Trim each end independently. All rows retain their center, and an
                        // untrimmed central row keeps the footprint connected and full-width.
                        var trim = y == bounds.Bottom + height / 2 ? 0 : (width - 1) / 2;
                        var left = bounds.Left + random.Next(trim + 1);
                        var right = bounds.Right - random.Next(trim + 1);
                        for (var x = left; x < right; x++)
                            chunks.Add(new(x, y));
                    }

                    regions.Add(new(bounds, chunks));
                    if (--remaining == 0)
                        return;
                }
            }
        }

        bool CanPlace(Box2i bounds)
        {
            foreach (var region in regions)
            {
                var other = region.Bounds;
                if (bounds.Left <= other.Right && bounds.Right >= other.Left &&
                    bounds.Bottom <= other.Top && bounds.Top >= other.Bottom)
                    return false;
            }

            for (var y = bounds.Bottom; y < bounds.Top; y++)
            for (var x = bounds.Left; x < bounds.Right; x++)
            {
                if (!eligible.Contains(new(x, y)))
                    return false;
            }

            return true;
        }
    }

    internal static bool IntersectsPoiClearance(Vector2i chunk, IReadOnlyList<SectorPoiSite> sites, bool hostileOnly)
    {
        var min = WorldGen.ChunkToWorldCoords(chunk);
        var max = min + new Vector2(WorldGen.ChunkSize);
        foreach (var site in sites)
        {
            if (hostileOnly && site.AllowsHostileDrones)
                continue;

            var closest = Vector2.Clamp(site.Position, min, max);
            if (Vector2.DistanceSquared(site.Position, closest) < site.ClearanceRadius * site.ClearanceRadius)
                return true;
        }

        return false;
    }
}
