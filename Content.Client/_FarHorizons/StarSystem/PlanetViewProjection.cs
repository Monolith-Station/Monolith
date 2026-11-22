using System.Numerics;

namespace Content.Client._FarHorizons.StarSystem;

internal readonly record struct PlanetViewProjection(Vector2 Center, float Radius)
{
    public static PlanetViewProjection Orbit(Vector2 viewCenter, Vector2 planetPosition,
        float parallax, float radius, float approachProgress = 0f) =>
        new(Vector2.Lerp(viewCenter, planetPosition, parallax * (1f - approachProgress)),
            GrowRadius(radius, approachProgress));

    public static float GrowRadius(float radius, float progress) =>
        radius * MathHelper.Lerp(1f, 20f, progress * progress);

    public Vector2 SurfacePoint(Vector2 entry) => Center + entry * Radius;

    public static PlanetViewProjection FromSurfacePoint(Vector2 point, Vector2 entry, float radius) =>
        new(point - entry * radius, radius);
}
