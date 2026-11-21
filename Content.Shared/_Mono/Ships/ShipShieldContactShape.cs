using System.Numerics;

namespace Content.Shared._Mono.Ships;

public readonly struct ShipShieldContactShape
{
    private readonly Vector3 _radii;
    private readonly Angle _rotation;
    private readonly float _xx;
    private readonly float _xy;
    private readonly float _yy;
    private readonly float _zz;

    public ShipShieldContactShape(Vector3 radii, Angle rotation)
    {
        _radii = radii;
        _rotation = rotation;
        var x = rotation.RotateVec(Vector2.UnitX);
        var a = radii.X * radii.X;
        var b = radii.Y * radii.Y;
        _xx = a * x.X * x.X + b * x.Y * x.Y;
        _xy = (a - b) * x.X * x.Y;
        _yy = a * x.Y * x.Y + b * x.X * x.X;
        _zz = radii.Z * radii.Z;
    }

    // Perram-Wertheim contact: maximize the common squared scale at tangency.
    // The ellipsoids overlap when the maximum is below one.
    public float Measure(ShipShieldContactShape other, Vector3 delta, out Vector3 axis)
    {
        var low = 0f;
        var high = 1f;
        for (var i = 0; i < 22; i++)
        {
            var a = (2f * low + high) / 3f;
            var b = (low + 2f * high) / 3f;
            if (At(other, delta, a, out _) < At(other, delta, b, out _))
                low = a;
            else
                high = b;
        }

        var measure = At(other, delta, (low + high) * 0.5f, out var direction);
        axis = direction == Vector3.Zero ? Vector3.UnitX : Vector3.Normalize(direction);
        return measure;

    }

    private float At(ShipShieldContactShape other, Vector3 delta, float t, out Vector3 direction)
    {
        var xx = MathHelper.Lerp(_xx, other._xx, t);
        var xy = MathHelper.Lerp(_xy, other._xy, t);
        var yy = MathHelper.Lerp(_yy, other._yy, t);
        var determinant = xx * yy - xy * xy;
        direction = new Vector3(
            (yy * delta.X - xy * delta.Y) / determinant,
            (xx * delta.Y - xy * delta.X) / determinant,
            delta.Z / MathHelper.Lerp(_zz, other._zz, t));
        return t * (1f - t) * Vector3.Dot(delta, direction);
    }

    public Vector3 Surface(Vector3 axis)
    {
        var xy = (-_rotation).RotateVec(new Vector2(axis.X, axis.Y));
        return Vector3.Normalize(new Vector3(xy, axis.Z) * _radii);
    }

    public float Support(Vector3 axis)
    {
        var xy = (-_rotation).RotateVec(new Vector2(axis.X, axis.Y));
        return (new Vector3(xy, axis.Z) * _radii).Length();
    }
}
