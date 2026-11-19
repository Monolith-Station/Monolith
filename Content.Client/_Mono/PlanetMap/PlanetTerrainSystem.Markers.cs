using System.Numerics;
using Content.Shared._Mono.PlanetMap;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Mono.PlanetMap;

// Points of interest we mark with PlanetMapMarkerComponent. The server sends these to every client,
// they can be drawn at any range for points of interest, landing pads, beacons or whatever.
// If the client hasn't been to planet it has no grid data to draw, so this lets you know where stuff is. Yes, bandaid solution.
public sealed partial class PlanetTerrainSystem
{
    private static readonly Vector2[] MarkerShape =
    [
        new(0f, -4f),
        new(4f, 0f),
        new(0f, 4f),
        new(-4f, 0f),
    ];

    private readonly Vector2[] _markerVerts = new Vector2[MarkerShape.Length + 1];

    /// <summary>
    /// Draws all the planet map markers on <paramref name="mapId"/> .
    /// </summary>
    public void DrawMarkers(DrawingHandleScreen handle, MapId mapId, Box2 viewBox, Matrix3x2 worldToView, Font font, float uiScale)
    {
        var query = EntityQueryEnumerator<PlanetMapMarkerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var marker, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var world = _transform.GetWorldPosition(xform);
            if (!viewBox.Contains(world))
                continue;

            var pos = Vector2.Transform(world, worldToView);

            for (var i = 0; i < MarkerShape.Length; i++)
            {
                _markerVerts[i] = pos + MarkerShape[i] * uiScale;
            }

            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, new ReadOnlySpan<Vector2>(_markerVerts, 0, MarkerShape.Length), marker.Color.WithAlpha(0.35f));

            _markerVerts[MarkerShape.Length] = _markerVerts[0];
            handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, _markerVerts, marker.Color);

            if (!marker.ShowLabel)
                continue;

            var label = marker.Label is { } loc ? Loc.GetString(loc) : MetaData(uid).EntityName;
            if (string.IsNullOrEmpty(label))
                continue;

            var dims = handle.GetDimensions(font, label, uiScale);
            handle.DrawString(font, pos + new Vector2(6f * uiScale, -dims.Y / 2f), label, uiScale, marker.Color);
        }
    }
}
