using System.Numerics;
using Robust.Shared.Random;

namespace Content.Shared._FarHorizons.StarSystem.Helpers;

public interface IStarSystemValueRange<T>
{
    public T RollValue(System.Random rand);
}

[DataDefinition]
public sealed partial class FloatRangeValue : IStarSystemValueRange<float>
{
    [DataField(required: true)] public float Min;
    [DataField(required: true)] public float Max;

    public FloatRangeValue(float min, float max)
    {
        Min = min;
        Max = max;
    }

    public float RollValue(System.Random rand) => rand.NextFloat(Min, Max);
}

[DataDefinition]
public sealed partial class IntRangeValue : IStarSystemValueRange<int>
{
    [DataField(required: true)] public int Min;
    [DataField(required: true)] public int Max;

    public IntRangeValue(int min, int max)
    {
        Min = min;
        Max = max;
    }

    public int RollValue(System.Random rand) => rand.Next(Min, Max);
}

[DataDefinition]
public sealed partial class ColorRangeValue : IStarSystemValueRange<Color>
{
    [DataField(required: true)] public Color Min;
    [DataField(required: true)] public Color Max;

    public ColorRangeValue(Color min, Color max)
    {
        Min = min;
        Max = max;
    }

    public Color RollValue(System.Random rand)
    {
        var vectorMin = new Vector4(Min.R, Min.G, Min.B, Min.A);
        var vectorMax = new Vector4(Max.R, Max.G, Max.B, Max.A);
        var factor = rand.NextFloat();
        var resultVector = Vector4.Lerp(vectorMin, vectorMax, factor);
        return new Color(resultVector.X, resultVector.Y, resultVector.Z, resultVector.W);
    }
}