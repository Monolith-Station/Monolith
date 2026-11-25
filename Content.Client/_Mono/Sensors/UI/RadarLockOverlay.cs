using System.Numerics;
using Content.Shared._Mono.Sensors;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._Mono.Sensors.UI;

public static class RadarLockOverlay
{
    private static readonly Color LockColor = Color.FromHex("#33FF66");
    private static readonly Color CoastColor = Color.FromHex("#88AA88");
    private static readonly Color LeadColor = Color.FromHex("#FFCC33");

    public static void Draw(
        DrawingHandleScreen handle,
        RadarLockStateDto? lockState,
        Matrix3x2 worldToView,
        float uiScale)
    {
        if (lockState is not { LockValid: true, TargetWorldPosition: { } tgtWorld })
            return;

        var timing = IoCManager.Resolve<IGameTiming>();
        var dt = Math.Clamp((float)(timing.CurTime - lockState.ServerTime).TotalSeconds, 0f, 0.75f);

        var predTgt = tgtWorld;
        if (lockState.TargetWorldVelocity is { } vel)
            predTgt += vel * dt;

        var q = Math.Clamp(lockState.Quality, 0f, 1f);
        var lockCol = (lockState.Coasting ? CoastColor : LockColor).WithAlpha(0.35f + 0.65f * q);
        var tgtScreen = Vector2.Transform(predTgt, worldToView);
        DrawCornerBrackets(handle, tgtScreen, 14f * uiScale, 6f * uiScale, lockCol);

        if (q < 0.25f)
            return;

        var leadAlpha = 0.25f + 0.75f * q;
        var lineCol = LeadColor.WithAlpha(0.55f * leadAlpha);
        var leadCol = LeadColor.WithAlpha(leadAlpha);

        void DrawOneLead(Vector2 leadWorld)
        {
            var predLead = leadWorld;
            if (lockState.TargetWorldVelocity is { } lv)
                predLead += lv * dt;

            var leadScreen = Vector2.Transform(predLead, worldToView);
            handle.DrawLine(tgtScreen, leadScreen, lineCol);
            DrawDiamond(handle, leadScreen, 5f * uiScale, leadCol);
        }

        if (lockState.WeaponLeads is { Count: > 0 })
        {
            foreach (var w in lockState.WeaponLeads)
            {
                DrawOneLead(w.LeadWorld);
            }
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
