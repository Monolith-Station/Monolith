using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Shared._Forge.WallPaint;

public static class WallPaintColor
{
    public const float MinSaturation = 0f;
    public const float MaxSaturation = 0.70f;
    public const float MinValue = 0.10f;
    public const float MaxValue = 1f;

    public static Color Clamp(Color color)
    {
        var hsv = Color.ToHsv(color);
        var saturation = MathHelper.Clamp(hsv.Y, MinSaturation, MaxSaturation);
        var value = MathHelper.Clamp(hsv.Z, MinValue, MaxValue);

        if (MathHelper.CloseTo(saturation, hsv.Y) &&
            MathHelper.CloseTo(value, hsv.Z))
        {
            return color;
        }

        return Color.FromHsv(new Vector4(hsv.X, saturation, value, hsv.W));
    }
}
