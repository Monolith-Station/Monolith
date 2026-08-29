using System.Numerics;
using Content.Client._FarHorizons.StarSystem;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Content.Shared.Shuttles.Components;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl
{
    private readonly List<BlipData> _beaconBlips = new();
    private readonly List<(EntityUid Planet, Vector2 Position, float Radius, bool InRange)> _planetHitboxes = new();

    public Action<EntityUid>? OnPlanetClick;

    private void DrawStarSystem(DrawingHandleScreen handle, Matrix3x2 worldToShuttle, Matrix3x2 shuttleToView, EntityUid? mapUid)
    {
        _planetHitboxes.Clear();

        if (!EntManager.TryGetComponent<StarSystemMapComponent>(mapUid, out var starSystem) ||
            starSystem.StarSystem == null)
            return;

        var worldToView = worldToShuttle * shuttleToView;
        var viewScale = MathF.Sqrt((worldToView.M11 * worldToView.M11) + (worldToView.M12 * worldToView.M12));

        var starPos = Vector2.Transform(starSystem.StarSystem.Star.Position, worldToView);
        var starRadius = Star.NAV_PIXEL_SIZE * starSystem.StarSystem.Star.Radius * viewScale;

        handle.DrawCircle(starPos, starRadius, starSystem.StarSystem.Star.Color.WithAlpha(0.5f));

        var bodies = new Dictionary<int, EntityUid>();
        var query = EntManager.AllEntityQueryEnumerator<PlanetBodyComponent>();
        while (query.MoveNext(out var uid, out var body))
        {
            if (body.StarSystemMap == mapUid)
                bodies[body.Index] = uid;
        }

        EntityUid? chargingPlanet = null;
        var chargingProgress = 0f;
        if (_coordinates is { } coordinates &&
            EntManager.TryGetComponent<TransformComponent>(coordinates.EntityId, out var coordXform) &&
            coordXform.GridUid is { } coordGrid &&
            EntManager.System<PlanetTransitSystem>().TryGetTransit(coordGrid, out var transit) &&
            transit.Phase == PlanetTransitPhase.Charging)
        {
            chargingPlanet = transit.Planet;
            var duration = (transit.PhaseEnd - transit.PhaseStart).TotalSeconds;
            var elapsed = (IoCManager.Resolve<IGameTiming>().CurTime - transit.PhaseStart).TotalSeconds;
            chargingProgress = duration > 0 ? Math.Clamp((float) (elapsed / duration), 0f, 1f) : 1f;
        }

        for (var i = 0; i < starSystem.StarSystem.Planets.Count; i++)
        {
            var planet = starSystem.StarSystem.Planets[i];
            var planetPos = Vector2.Transform(planet.Position, worldToView);
            var planetRadius = Planet.NAV_PIXEL_SIZE * planet.Radius * viewScale;
            handle.DrawCircle(planetPos, planetRadius, Color.Gray.WithAlpha(0.5f));

            if (!bodies.TryGetValue(i, out var body))
                continue;

            var hitRadius = MathF.Max(planetRadius, 6f * UIScale);
            var inRange = false;
            if (_coordinates is { } shuttleCoordinates &&
                EntManager.TryGetComponent<TransformComponent>(shuttleCoordinates.EntityId, out var shuttleXform) &&
                shuttleXform.GridUid is { } shuttleGrid &&
                EntManager.TryGetComponent<TransformComponent>(body, out var planetXform) &&
                shuttleXform.MapUid == planetXform.MapUid &&
                EntManager.TryGetComponent<PlanetBodyComponent>(body, out var planetBody))
            {
                var distance = _transform.GetWorldPosition(shuttleGrid) - _transform.GetWorldPosition(body);
                inRange = distance.LengthSquared() <= planetBody.ApproachRadius * planetBody.ApproachRadius;
            }

            _planetHitboxes.Add((body, planetPos / UIScale, hitRadius / UIScale, inRange));

            var hovered = _isMouseInside && (_lastMousePos - planetPos / UIScale).LengthSquared() <=
                hitRadius * hitRadius / (UIScale * UIScale);
            if (hovered)
            {
                var radius = hitRadius + 3f * UIScale;
                var color = inRange ? Color.White.WithAlpha(0.8f) : Color.Red.WithAlpha(0.5f);
                DrawPlanetRing(handle, planetPos, radius, MathF.Max(UIScale, 1f), color);
            }

            if (_primingPlanet == body && PrimingRemaining is { } remaining)
            {
                var radius = hitRadius + 5f * UIScale;
                DrawPlanetRing(handle, planetPos, radius, MathF.Max(UIScale, 1f), Color.White.WithAlpha(0.15f));
                DrawPlanetProgress(handle, planetPos, radius, 1f - remaining / PlanetPrimingSeconds, Color.Cyan);
            }

            if (chargingPlanet == body)
            {
                var radius = hitRadius + 8f * UIScale;
                DrawPlanetRing(handle, planetPos, radius, MathF.Max(UIScale, 1f), Color.LimeGreen.WithAlpha(0.15f));
                DrawPlanetProgress(handle, planetPos, radius, chargingProgress, Color.LimeGreen);
                DrawPlanetProgress(handle, planetPos, radius + 1f, chargingProgress, Color.LimeGreen);
            }
        }
    }

    private void DrawPlanetLaunchWarnings(DrawingHandleScreen handle, Matrix3x2 worldToView, EntityUid? observerMap)
    {
        if (observerMap == null)
            return;

        var query = EntManager.AllEntityQueryEnumerator<PlanetTransitComponent, MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var transit, out var grid, out var xform))
        {
            if (transit.Direction != PlanetTransitDirection.Ascent ||
                transit.Phase != PlanetTransitPhase.Charging ||
                !EntManager.TryGetComponent<PlanetBodyComponent>(transit.Planet, out var planet) ||
                planet.SurfaceNetwork is not { } surface ||
                xform.MapUid != observerMap && !MapBelongsToSurface(observerMap.Value, surface))
            {
                continue;
            }

            var gridToView = _transform.GetWorldMatrix(uid) * worldToView;
            var center = Vector2.Transform(grid.LocalAABB.Center, gridToView);
            var radius = MathF.Max(grid.LocalAABB.Size.Length() * MinimapScale * 0.5f + 6f * UIScale,
                10f * UIScale);
            var duration = (transit.PhaseEnd - transit.PhaseStart).TotalSeconds;
            var elapsed = (IoCManager.Resolve<IGameTiming>().CurTime - transit.PhaseStart).TotalSeconds;
            var progress = duration > 0 ? Math.Clamp((float) (elapsed / duration), 0f, 1f) : 1f;

            DrawPlanetRing(handle, center, radius, MathF.Max(UIScale, 1f), Color.Orange.WithAlpha(0.2f));
            DrawPlanetProgress(handle, center, radius, progress, Color.OrangeRed);
            DrawPlanetProgress(handle, center, radius + 1f, progress, Color.OrangeRed);
        }
    }

    private bool MapBelongsToSurface(EntityUid map, EntityUid surface)
    {
        if (EntManager.TryGetComponent<CEZMapComponent>(map, out var zMap))
            return zMap.NetworkUid == surface;

        if (!EntManager.TryGetComponent<CEZTransitMapComponent>(map, out var transit))
            return false;

        return transit.LowerMap is { } lower &&
               EntManager.TryGetComponent<CEZMapComponent>(lower, out var lowerZ) &&
               lowerZ.NetworkUid == surface;
    }

    private static void DrawPlanetRing(DrawingHandleScreen handle,
        Vector2 center,
        float radius,
        float thickness,
        Color color)
    {
        var inner = Math.Max(radius - thickness, 0f);
        var segments = Math.Clamp((int) (radius * 0.75f), 24, 128);
        var vertices = new Vector2[(segments + 1) * 2];
        for (var i = 0; i <= segments; i++)
        {
            var angle = MathF.Tau * (i % segments) / segments;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            vertices[i * 2] = center + direction * radius;
            vertices[i * 2 + 1] = center + direction * inner;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, vertices, color);
    }

    private static void DrawPlanetProgress(DrawingHandleScreen handle,
        Vector2 center,
        float radius,
        float progress,
        Color color)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        if (progress <= 0f)
            return;

        var segments = Math.Max(2, (int) (64 * progress));
        var vertices = new Vector2[segments + 1];
        for (var i = 0; i <= segments; i++)
        {
            var angle = -MathF.PI / 2f + MathF.Tau * progress * i / segments;
            vertices[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, vertices, color);
    }

    private bool TryGetPlanetAt(Vector2 position, out EntityUid planet)
        => TryGetPlanetAt(position, out planet, out _);

    private bool TryGetPlanetAt(Vector2 position, out EntityUid planet, out bool inRange)
    {
        foreach (var hitbox in _planetHitboxes)
        {
            if ((position - hitbox.Position).LengthSquared() > hitbox.Radius * hitbox.Radius)
                continue;

            planet = hitbox.Planet;
            inRange = hitbox.InRange;
            return true;
        }

        planet = default;
        inRange = false;
        return false;
    }

    // code stolen from IFF beacon rendering code for grids
    private void DrawIFFBeacons(DrawingHandleScreen handle, Matrix3x2 worldToView, MapCoordinates mapPos, EntityUid? mapUid)
    {
        if (!ShowIFF || mapUid == null)
            return;

        _beaconBlips.Clear();

        var uiXCentre = (int)Width / 2;
        var uiYCentre = (int)Height / 2;
        var blipSize = RadarBlipSize * 0.7f;

        var query = EntManager.AllEntityQueryEnumerator<IFFComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var iff, out var xform))
        {
            if (xform.MapUid != mapUid ||
                EntManager.HasComponent<MapGridComponent>(uid) ||
                (iff.Flags & IFFFlags.Hide) != 0x0)
                continue;

            if (_shuttles.GetIFFLabel(uid, component: iff) is not { } label)
                continue;

            var color = _shuttles.GetIFFColor(uid, self: false, iff);
            var worldPos = _transform.GetWorldPosition(uid);
            var uiPosition = Vector2.Transform(worldPos, worldToView) / UIScale;

            var uiXOffset = uiPosition.X - uiXCentre;
            var uiYOffset = uiPosition.Y - uiYCentre;
            var uiDistance = (int)Math.Sqrt(Math.Pow(uiXOffset, 2) + Math.Pow(uiYOffset, 2));
            var uiX = uiXCentre * uiXOffset / uiDistance;
            var uiY = uiYCentre * uiYOffset / uiDistance;

            var isOutsideRadarCircle = uiDistance > Math.Abs(uiX) && uiDistance > Math.Abs(uiY);
            if (isOutsideRadarCircle)
            {
                uiPosition = new Vector2(
                    x: uiXCentre * uiXOffset / uiDistance * 0.95f + uiXCentre,
                    y: uiYCentre * uiYOffset / uiDistance * 0.95f + uiYCentre
                );
            }

            NfAddBlipToList(_beaconBlips, isOutsideRadarCircle, uiPosition, uiXCentre, uiYCentre, color);

            var distance = Vector2.Distance(worldPos, mapPos.Position);
            var displayedDistance = distance < 50f ? $"{distance:0.0}" : distance < 1000 ? $"{distance:0}" : $"{distance / 1000:0.0}k";

            var lines = Loc.GetString("shuttle-console-iff-label", ("name", label), ("distance", displayedDistance)).Split('\n');
            var mainLabel = lines[0];

            var labelOffset = new Vector2(blipSize, -handle.GetDimensions(Font, mainLabel, 0.9f).Y * 0.5f);
            handle.DrawString(Font, (uiPosition + labelOffset) * UIScale, mainLabel, UIScale * 0.9f, color);

            if (!ShowIFFDetailed)
                continue;

            var stackOffset = labelOffset.Y + handle.GetDimensions(Font, mainLabel, 0.9f).Y;

            if (lines.Length > 1)
            {
                handle.DrawString(Font, (uiPosition + new Vector2(labelOffset.X, stackOffset)) * UIScale, lines[1], UIScale * 0.7f, color);
                stackOffset += handle.GetDimensions(Font, lines[1], 0.7f).Y;
            }

            var coordsText = $"({worldPos.X:0.0}, {worldPos.Y:0.0})";
            handle.DrawString(Font, (uiPosition + new Vector2(labelOffset.X, stackOffset)) * UIScale, coordsText, UIScale * 0.7f, color);
        }

        NfDrawBlips(handle, _beaconBlips);
    }
}
