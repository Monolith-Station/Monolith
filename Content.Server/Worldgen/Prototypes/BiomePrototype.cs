using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Server.Worldgen.Prototypes;

/// <summary>
///     This is a prototype for biome selection, allowing the component list of a chunk to be amended based on the output
///     of noise channels at that location.
/// </summary>
[Prototype("spaceBiome")]
public sealed partial class BiomePrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc />
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<BiomePrototype>))] // Frontier: EntityPrototype<BiomePrototype
    public string[]? Parents { get; private set; }

    /// <inheritdoc />
    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    /// <inheritdoc />
    [IdDataField]
    public string ID { get; private set; } = default!;

    // Frontier: distances
    /// <summary>
    ///     The valid range of biome coordinate lengths (3000, 4000) => 5000
    ///     Chunks with center points within this range may be generated with this biome.
    /// </summary>
    [ViewVariables]
    private Vector2? _distanceRange;

    /// <summary>
    ///     Accessor for range
    /// </summary>
    [DataField]
    public Vector2? DistanceRange
    {
        get { return _distanceRange; }
        private set
        {
            _distanceRange = value;

            if (value == null)
                DistanceRangeSquared = null;
            else
                DistanceRangeSquared = value * value;
        }
    }

    [ViewVariables]
    public Vector2? DistanceRangeSquared { get; private set; }
    // Frontier: distances

    /// <summary>
    ///     The valid ranges of noise values under which this biome can be picked.
    /// </summary>
    [DataField(required: true)] // EE/Mono
    public Dictionary<string, List<Vector2>> NoiseRanges = default!;

    /// <summary>
    ///     Higher priority biomes get picked before lower priority ones.
    /// </summary>
    [DataField(required: true)] // EE/Mono
    public int Priority { get; private set; }

    /// <summary>
    ///     The components that get added to the target map.
    /// </summary>
    [DataField] // EE/Mono
    [AlwaysPushInheritance]
    public ComponentRegistry ChunkComponents = new();

    // Mono / EE start - Specific chunk generation
    /// <summary>
    ///     Minimum X coordinate value to spawn this biome.
    /// </summary>
    [DataField]
    public int? MinX;

    /// <summary>
    ///     Minimum Y coordinate value to spawn this biome.
    /// </summary>
    [DataField]
    public int? MinY;

    /// <summary>
    ///     Maximum X coordinate value to spawn this biome.
    /// </summary>
    [DataField]
    public int? MaxX;

    /// <summary>
    ///     Maximum Y coordinate value to spawn this biome.
    /// </summary>
    [DataField]
    public int? MaxY;
    // Mono / EE end

    /// <summary>Additional rectangular options in chunk coordinates, with exclusive right and top edges.</summary>
    [DataField]
    [NeverPushInheritance]
    public List<Box2i> ChunkRegions { get; private set; } = [];

    /// <summary>Debris generation can place hostile drone spawners in this biome.</summary>
    [DataField]
    public bool HostileDroneZone { get; private set; }

    /// <summary>Permits POI placement in this biome's band by carving a gap in its hostile chunks.</summary>
    [DataField]
    public bool AllowPoiClearings { get; private set; }

    public bool HasFixedChunkRange => MinX.HasValue && MaxX.HasValue && MinY.HasValue && MaxY.HasValue;

    public bool MatchesGeometry(Vector2i chunk, float centerLengthSquared)
    {
        if (DistanceRangeSquared is { } range && (centerLengthSquared < range.X || centerLengthSquared > range.Y))
            return false;

        if (HasFixedChunkRange && (chunk.X < MinX || chunk.X > MaxX || chunk.Y < MinY || chunk.Y > MaxY))
            return false;

        if (ChunkRegions.Count == 0)
            return true;

        foreach (var region in ChunkRegions)
        {
            if (chunk.X >= region.Left && chunk.X < region.Right && chunk.Y >= region.Bottom && chunk.Y < region.Top)
                return true;
        }

        return false;
    }

    /// <summary>Returns a radius enclosing every world coordinate belonging to this biome's chunks.</summary>
    public double GetOuterGeometryRadius()
    {
        var radius = DistanceRange is { } range ? range.Y + Math.Sqrt(2) * WorldGen.ChunkSize / 2 : double.PositiveInfinity;
        if (HasFixedChunkRange)
            radius = Math.Min(radius, GetRadius(MinX!.Value, MaxX!.Value + 1, MinY!.Value, MaxY!.Value + 1));

        if (ChunkRegions.Count > 0)
        {
            var outermost = 0d;
            foreach (var region in ChunkRegions)
                outermost = Math.Max(outermost, GetRadius(region.Left, region.Right, region.Bottom, region.Top));
            radius = Math.Min(radius, outermost);
        }

        return radius;

        static double GetRadius(int left, int right, int bottom, int top)
        {
            var x = Math.Max(Math.Abs((double) left), Math.Abs((double) right)) * WorldGen.ChunkSize;
            var y = Math.Max(Math.Abs((double) bottom), Math.Abs((double) top)) * WorldGen.ChunkSize;
            return Math.Sqrt(x * x + y * y);
        }
    }

    //TODO: Get someone to make this a method on componentregistry that does it Correctly.
    /// <summary>
    ///     Applies the worldgen config to the given target (presumably a map.)
    /// </summary>
    public void Apply(EntityUid target, ISerializationManager serialization, IEntityManager entityManager)
    {
        // Add all components required by the prototype. Engine update for this whenst.
        foreach (var data in ChunkComponents.Values)
        {
            var comp = (Component) serialization.CreateCopy(data.Component, notNullableOverride: true);
            entityManager.AddComponent(target, comp);
        }
    }
}

