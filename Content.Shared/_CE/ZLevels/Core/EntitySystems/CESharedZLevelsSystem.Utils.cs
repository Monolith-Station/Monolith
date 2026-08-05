using System.Numerics;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{

    // I feel like Ilya will laugh at me if I don't move this out of the method.
    private static readonly Vector2[] SideNormals = { new(0, -1), new(1, 0), new(0, 1), new(-1, 0) };

    /// <summary>
    /// Gets the swept area of two boxes as a list of boxes.
    /// Note that this function was written for grids; should work for anything else though.
    /// </summary>
    public static void GetSweptArea(Box2Rotated previous, Box2Rotated current, List<Box2Rotated> sweptArea)
    {
        // First, calculate the current depth of each vertice for all four sides.

        Span<Vector2> previousCorners = new[] { previous.BottomLeft, previous.BottomRight, previous.TopRight, previous.TopLeft };
        Span<Vector2> currentCorners = new[] { current.BottomLeft, current.BottomRight, current.TopRight, current.TopLeft };

        Span<float> depths = new float[4];

        for (var i = 0; i < 4; i++)
        {
            var start = previousCorners[i];
            var end = previousCorners[(i + 1) % 4];

            var extrudeDistance = 0f;
            foreach (var corner in currentCorners)
            {
                var depth = ExtrusionDepth(start, end, corner);
                if (depth > extrudeDistance)
                    extrudeDistance = depth;
            }

            if (extrudeDistance > 1e-4f)
            {
                depths[i] = extrudeDistance;
            }
        }

        // Then, use those depths to extrude a new box from each side.

        var p = previous.Box;
        Vector2[] local = { p.BottomLeft, p.BottomRight, p.TopRight, p.TopLeft };

        for (var i = 0; i < 4; i++)
        {
            if (depths[i] <= 0f)
                continue;

            var a = local[i];
            var b = local[(i + 1) % 4] + SideNormals[i] * depths[i];
            sweptArea.Add(new Box2Rotated(new Box2(Vector2.Min(a, b), Vector2.Max(a, b)), previous.Rotation, previous.Origin));
        }

        for (var i = 0; i < 4; i++)
        {
            var j = (i + 1) % 4;
            if (depths[i] <= 0f || depths[j] <= 0f)
                continue;

            var c = local[j];
            var p1 = c + SideNormals[i] * depths[i];
            var p2 = c + SideNormals[j] * depths[j];
            sweptArea.Add(new Box2Rotated(new Box2(Vector2.Min(p1, p2), Vector2.Max(p1, p2)), previous.Rotation, previous.Origin));
        }
    }

    public static float ExtrusionDepth(Vector2 sideStart, Vector2 sideEnd, Vector2 vertex)
    {
        var edge = sideEnd - sideStart;
        var offset = vertex - sideStart;

        var length = edge.Length();
        return length > 0f ? (edge.Y * offset.X - edge.X * offset.Y) / length : 0f;
    }
}
