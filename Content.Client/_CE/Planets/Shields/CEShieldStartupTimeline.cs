namespace Content.Client._CE.Planets.Shields;

internal readonly record struct CEShieldStartupTimeline(float Burst, float End, float DrainEnd)
{
    public static float ClampMoteSpeed(float speed) => float.IsFinite(speed) ? Math.Max(0.001f, speed) : 0.001f;

    public static CEShieldStartupTimeline From(float burstSeconds, float endSeconds, int startCount, int endCount,
        float endSpeed)
    {
        var burst = Math.Max(0.001f, burstSeconds);
        var end = Math.Max(burst + 0.001f, endSeconds);
        var drain = startCount > 0 || endCount > 0
            ? 1f / (ClampMoteSpeed(endSpeed) * 0.037f)
            : 0f;
        return new(burst, end, end + drain);
    }
}
