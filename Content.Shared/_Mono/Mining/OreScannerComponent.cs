using System.Numerics;
using Content.Shared.Shuttles.BUIStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Mining;

[RegisterComponent]
public sealed partial class OreScannerComponent : Component
{
    public const float ScanRange = 64f;
    [DataField] public float Range = ScanRange;
}

[Serializable, NetSerializable]
public enum OreScannerUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class OreScannerState(NavInterfaceState navigation, NetEntity surface, OreDepositScan[] deposits) : BoundUserInterfaceState
{
    public readonly NavInterfaceState Navigation = navigation;
    public readonly NetEntity Surface = surface;
    public readonly OreDepositScan[] Deposits = deposits;
}

[Serializable, NetSerializable]
public sealed class OreDepositScan(string name, Color color, Vector2[] boundary, int remaining, float saturation, float rate, string? vvPath = null)
{
    public readonly string Name = name;
    public readonly Color Color = color;
    public readonly Vector2[] Boundary = boundary;
    public readonly int Remaining = remaining;
    public readonly float Saturation = saturation;
    public readonly float Rate = rate;
}

public static class OreDepositGeometry
{
    public static bool Contains(Vector2[] boundary, Vector2 point)
    {
        var inside = false;
        for (int i = 0, j = boundary.Length - 1; i < boundary.Length; j = i++)
        {
            var a = boundary[i];
            var b = boundary[j];
            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }
}
