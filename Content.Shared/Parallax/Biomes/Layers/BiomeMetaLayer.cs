using Robust.Shared.Noise;
using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;

namespace Content.Shared.Parallax.Biomes.Layers;

/// <summary>
/// Contains more biome layers recursively via a biome template.
/// Can be used for sub-biomes.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BiomeMetaLayer : IBiomeLayer
{
    [DataField]
    public List<IBiomeLayer>? Layers;

    [DataField]
    public float OriginBiasRadius;

    [DataField]
    public float OriginBiasStrength;

    public static float GetOriginBias(float x, float y, float radius, float strength)
    {
        if (radius <= 0f || strength == 0f)
            return 0f;

        var distanceSquared = x * x + y * y;
        if (distanceSquared >= radius * radius)
            return 0f;

        var t = MathF.Sqrt(distanceSquared) / radius;
        return strength * (1f - t * t * (3f - 2f * t));
    }

    [DataField("noise")]
    public FastNoiseLite Noise { get; private set; } = new(0);

    /// <inheritdoc/>
    [DataField("threshold")]
    public float Threshold { get; private set; } = -1f;

    /// <inheritdoc/>
    [DataField("invert")]
    public bool Invert { get; private set; }

    [DataField(required: true)]
    public ProtoId<BiomeTemplatePrototype> Template = string.Empty;
}
