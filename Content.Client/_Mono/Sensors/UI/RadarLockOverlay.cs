using System.Numerics;
using Content.Shared._Mono.Sensors;
using Robust.Client.Graphics;

namespace Content.Client._Mono.Sensors.UI;

public static class RadarLockOverlay
{
    private static readonly Color LockColor = Color.FromHex("#33FF66");
    private static readonly Color LeadColor = Color.FromHex("#FFCC33");
    private static readonly Color LineColor = Color.FromHex("#FFCC33").WithAlpha(0.55f);

    public static void Draw(DrawingHandleScreen handle, RadarLockStateDto? lockState, Matrix3x2 worldToView, float uiScale)
    {
        if (lockState is not { LockValid: true, TargetWorldPosition: { } tgtWorld })
            return;

        var tgtScreen = Vector2.Transform(tgtWorld, worldToView);
        DrawCornerBrackets(handle, tgtScreen, 14f * uiScale, 6f * uiScale, LockColor);

        void DrawOneLead(Vector2 leadWorld)
        {
            var leadScreen = Vector2.Transform(leadWorld, worldToView);
            handle.DrawLine(tgtScreen, leadScreen, LineColor);
            DrawDiamond(handle, leadScreen, 5f * uiScale, LeadColor);
        }

        if (lockState.WeaponLeads is { Count: > 0 })
        {
            foreach (var w in lockState.WeaponLeads)
                DrawOneLead(w.LeadWorld);
        }
        else if (lockState.AverageLeadWorld is { } avg)
        {
            DrawOneLead(avg);
        }
    }

    public static void DrawCornerBrackets(DrawingHandleScreen handle, Vector2 center, float size, float arm, Color color)
    {
        handle.DrawLine(center + new Vector2(-size, -size), center + new Vector2(-size + arm, -size), color);
        handle.DrawLine(center + new Vector2(-size, -size), center + new Vector2(-size, -size + arm), color);
        handle.DrawLine(center + new Vector2(size, -size), center + new Vector2(size - arm, -size), color);
        handle.DrawLine(center + new Vector2(size, -size), center + new Vector2(size, -size + arm), color);
        handle.DrawLine(center + new Vector2(-size, size), center + new Vector2(-size + arm, size), color);
        handle.DrawLine(center + new Vector2(-size, size), center + new Vector2(-size, size - arm), color);
        handle.DrawLine(center + new Vector2(size, size), center + new Vector2(size - arm, size), color);
        handle.DrawLine(center + new Vector2(size, size), center + new Vector2(size, size - arm), color);
    }

    public static void DrawDiamond(DrawingHandleScreen handle, Vector2 center, float r, Color color)
    {
        var t = center + new Vector2(0, -r);
        var right = center + new Vector2(r, 0);
        var b = center + new Vector2(0, r);
        var left = center + new Vector2(-r, 0);
        handle.DrawLine(t, right, color);
        handle.DrawLine(right, b, color);
        handle.DrawLine(b, left, color);
        handle.DrawLine(left, t, color);
    }
}
