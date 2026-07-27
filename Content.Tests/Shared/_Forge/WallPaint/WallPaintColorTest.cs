using Content.Shared._Forge.WallPaint;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared._Forge.WallPaint;

[TestFixture]
public sealed class WallPaintColorTest
{
    [Test]
    public void ClampLimitsSaturation()
    {
        var clamped = WallPaintColor.Clamp(Color.Red);
        var hsv = Color.ToHsv(clamped);

        Assert.That(hsv.Y, Is.EqualTo(WallPaintColor.MaxSaturation).Within(0.0001f));
        Assert.That(hsv.Z, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void ClampLimitsMinimumValue()
    {
        var clamped = WallPaintColor.Clamp(Color.Black);
        var hsv = Color.ToHsv(clamped);

        Assert.That(hsv.Y, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(hsv.Z, Is.EqualTo(WallPaintColor.MinValue).Within(0.0001f));
    }

    [Test]
    public void ClampKeepsValidColor()
    {
        var color = Color.FromHsv(new(0.5f, 0.35f, 0.5f, 0.8f));
        var clamped = WallPaintColor.Clamp(color);

        Assert.That(MathHelper.CloseToPercent(clamped, color));
    }
}
