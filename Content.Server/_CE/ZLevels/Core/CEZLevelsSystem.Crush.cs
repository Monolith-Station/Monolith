using System.Numerics;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    private readonly Vector2[] _crushClipA = new Vector2[3 * PhysicsConstants.MaxPolygonVertices];
    private readonly Vector2[] _crushClipB = new Vector2[3 * PhysicsConstants.MaxPolygonVertices];

    internal float GetCrushOverlap(Entity<MapGridComponent> first, Entity<MapGridComponent> second, out Vector2 epicentre)
    {
        epicentre = default;
        var firstMatrix = _transform.GetWorldMatrix(first.Owner);
        var secondMatrix = _transform.GetWorldMatrix(second.Owner);
        var bounds = secondMatrix.TransformBox(second.Comp.LocalAABB);
        var total = 0f;
        var largest = 0f;
        foreach (var tile in _map.GetTilesIntersecting(first.Owner, first.Comp, bounds))
        {
            var firstCount = WriteTileFootprint(tile, first.Comp.TileSize, firstMatrix, _shipGroundFootprint);
            var hull = _shipGroundFootprint.AsSpan(0, firstCount);
            foreach (var other in _map.GetTilesIntersecting(second.Owner, second.Comp, GetFootprintBounds(hull)))
            {
                var secondCount = WriteTileFootprint(other, second.Comp.TileSize, secondMatrix, _groundFootprint);
                var area = ClipCrushFootprints(hull, _groundFootprint.AsSpan(0, secondCount), out var centre);
                total += area;
                if (area <= largest)
                    continue;

                largest = area;
                epicentre = centre;
            }
        }

        return total;
    }

    private float ClipCrushFootprints(ReadOnlySpan<Vector2> hull, ReadOnlySpan<Vector2> clip, out Vector2 centre)
    {
        centre = default;
        var winding = 0f;
        for (var i = 1; i < clip.Length - 1; i++)
            winding += Vector2Helpers.Cross(clip[i] - clip[0], clip[i + 1] - clip[0]);

        if (winding == 0f)
            return 0f;

        var sign = MathF.Sign(winding);
        var input = _crushClipA;
        var output = _crushClipB;
        hull.CopyTo(input);
        var count = hull.Length;
        for (var i = 0; i < clip.Length && count >= 3; i++)
        {
            var start = clip[i];
            var edge = clip[(i + 1) % clip.Length] - start;
            var write = 0;
            var previous = input[count - 1];
            var previousDistance = sign * Vector2Helpers.Cross(edge, previous - start);
            for (var j = 0; j < count; j++)
            {
                var current = input[j];
                var distance = sign * Vector2Helpers.Cross(edge, current - start);
                if ((distance < 0f && previousDistance > 0f) || (distance > 0f && previousDistance < 0f))
                    output[write++] = Vector2.Lerp(previous, current, previousDistance / (previousDistance - distance));

                if (distance >= 0f)
                    output[write++] = current;

                previous = current;
                previousDistance = distance;
            }

            (input, output) = (output, input);
            count = write;
        }

        var origin = input[0];
        var doubleArea = 0f;
        var moment = Vector2.Zero;
        for (var i = 1; i < count - 1; i++)
        {
            var a = input[i] - origin;
            var b = input[i + 1] - origin;
            var cross = Vector2Helpers.Cross(a, b);
            doubleArea += cross;
            moment += (a + b) * cross;
        }

        if (MathF.Abs(doubleArea) <= 0.000002f)
            return 0f;

        centre = origin + moment / (3f * doubleArea);
        return MathF.Abs(doubleArea) * 0.5f;
    }
}
