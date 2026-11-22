using System.Numerics;
using Content.Shared._Mono.Sensors;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Mono.Sensors.UI;

public sealed class SensorsArcDisplay : Control
{
    private readonly List<(float CenterDeg, float WidthDeg, Color Color)> _arcs = new();
    private readonly List<float> _contactBearings = new();

    public SensorsArcDisplay()
    {
        MinSize = new Vector2(180, 180);
        HorizontalExpand = false;
        VerticalExpand = false;
    }

    public void Update(
        IEnumerable<SensorHardwareDto> hardware,
        IEnumerable<SensorContactDto> contacts,
        float gridWorldRotationDegrees,
        IReadOnlyDictionary<NetEntity, float>? predictedOffsets = null)
    {
        _arcs.Clear();
        _contactBearings.Clear();

        foreach (var h in hardware)
        {
            if (!h.Enabled || !h.Powered)
                continue;
            if (h.ArcDegrees >= 360f)
                continue;

            var offset = predictedOffsets != null && predictedOffsets.TryGetValue(h.Entity, out var pred)
                ? pred
                : h.ArcOffset;

            var worldCenter = h.GridRelativeArc
                ? gridWorldRotationDegrees + offset
                : offset;
            var color = h.SensorType switch
            {
                SensorType.ActiveRadar => Color.FromHex("#33FF66").WithAlpha(0.35f),
                SensorType.PassiveRadar => Color.FromHex("#66CCFF").WithAlpha(0.30f),
                SensorType.Infrared => Color.FromHex("#FF6644").WithAlpha(0.30f),
                _ => Color.Lime.WithAlpha(0.25f),
            };

            var displayCenter = WorldToDisplay(worldCenter);
            _arcs.Add((displayCenter, h.ArcDegrees, color));
        }

        foreach (var c in contacts)
        {
            _contactBearings.Add(c.BearingDegrees);
        }
    }

    private static float WorldToDisplay(float worldDeg)
    {
        var d = (90f - worldDeg) % 360f;
        if (d < 0f)
            d += 360f;
        return d;
    }

    public static float DisplayToWorld(float displayDeg)
    {
        return WorldToDisplay(displayDeg);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = PixelSize;
        if (size.X < 4 || size.Y < 4)
            return;

        var center = size / 2f;
        var radius = MathF.Min(size.X, size.Y) * 0.48f;
        var arcRadius = radius * 0.5f;

        handle.DrawCircle(center, radius, Color.FromHex("#0a0a0a"), filled: true);
        handle.DrawCircle(center, radius, Color.FromHex("#1a3a1a"), filled: false);

        handle.DrawLine(center - new Vector2(radius, 0), center + new Vector2(radius, 0), Color.FromHex("#1a3a1a"));
        handle.DrawLine(center - new Vector2(0, radius), center + new Vector2(0, radius), Color.FromHex("#1a3a1a"));

        foreach (var (centerDeg, widthDeg, color) in _arcs)
        {
            DrawCone(handle, center, arcRadius, centerDeg, widthDeg, color);
        }

        foreach (var bearing in _contactBearings)
        {
            var dir = BearingToScreen(bearing);
            handle.DrawLine(center, center + dir * radius, Color.Lime.WithAlpha(0.85f));
        }

        handle.DrawCircle(center, 3f, Color.Lime, filled: true);
    }

    private static void DrawCone(
        DrawingHandleScreen handle,
        Vector2 center,
        float radius,
        float centerDeg,
        float widthDeg,
        Color color)
    {
        const int segments = 24;
        var half = widthDeg * 0.5f;
        var verts = new Vector2[segments + 2];
        verts[0] = center;

        for (var i = 0; i <= segments; i++)
        {
            var t = i / (float)segments;
            var deg = centerDeg - half + widthDeg * t;
            verts[i + 1] = center + BearingToScreen(deg) * radius;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, verts, color);

        var edge = Color.White.WithAlpha(0.25f);
        handle.DrawLine(center, verts[1], edge);
        handle.DrawLine(center, verts[^1], edge);
    }

    private static Vector2 BearingToScreen(float displayDeg)
    {
        var rad = displayDeg * MathF.PI / 180f;
        return new Vector2(MathF.Sin(rad), -MathF.Cos(rad));
    }
}
