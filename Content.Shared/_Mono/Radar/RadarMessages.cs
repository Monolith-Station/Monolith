using System.Linq;
using System.Numerics;
using System.Xml;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Radar;

[Serializable, NetSerializable]
public enum RadarBlipShape
{
    Circle,
    Square,
    GridAlignedBox,
    Triangle,
    Star,
    Diamond,
    Hexagon,
    Arrow,
    Ring
}

[Serializable, NetSerializable]
public sealed class GiveBlipsEvent : EntityEventArgs
{
    /// <summary>
    /// Palette of blip configs, basically an int->config map.
    /// </summary>
    public readonly List<BlipConfig> ConfigPalette;

    /// <summary>
    /// Blips are now (position, velocity, scale, color, shape).
    /// </summary>
    public readonly List<BlipNetData> Blips;

    /// <summary>
    /// Hitscan lines to display on the radar as (start position, end position, thickness, color).
    /// </summary>
    public readonly List<HitscanNetData> HitscanLines;

    public GiveBlipsEvent(
        List<BlipConfig> configPalette,
        List<BlipNetData> blips,
        List<HitscanNetData> hitscans)
    {
        ConfigPalette = configPalette;
        Blips = blips;
        HitscanLines = hitscans;
    }
}

[Serializable, NetSerializable]
public sealed class RequestBlipsEvent(NetEntity radar) : EntityEventArgs
{
    public readonly NetEntity Radar = radar;
}

[Serializable, NetSerializable]
public sealed class BlipRemovalEvent(NetEntity netBlipUid) : EntityEventArgs
{
    public readonly NetEntity NetBlipUid = netBlipUid;
}

[Serializable, NetSerializable]
public record struct BlipNetData
(
    NetEntity Uid,
    NetCoordinates Position,
    Vector2 Vel,
    Angle Rotation,
    ushort ConfigIndex,
    ushort? OnGridConfigIndex

);

[Serializable, NetSerializable]
public record struct HitscanNetData(Vector2 Start, Vector2 End, float Thickness, Color Color);

[Serializable, NetSerializable, DataDefinition]
public partial struct BlipConfig : IEquatable<BlipConfig>
{
    [DataField]
    public Box2 Bounds = new Box2(-0.5f, -0.5f, 0.5f, 0.5f);

    [DataField]
    public Color Color = Color.OrangeRed;

    [DataField]
    public RadarBlipShape Shape = RadarBlipShape.Circle;

    [DataField]
    public bool RespectZoom = false;

    [DataField]
    public bool Rotate = false;

    public BlipConfig() { }

    public readonly override bool Equals(object? obj)
    {
        return obj is BlipConfig other && Equals(other);
    }

    public readonly bool Equals(BlipConfig other)
    {
        return Shape == other.Shape
            && RespectZoom == other.RespectZoom
            && Rotate == other.Rotate
            && Color == other.Color
            && Bounds == other.Bounds;
    }

    public readonly override int GetHashCode()
    {
        throw new NotSupportedException("BlipConfig should not be used as a dictionary key.");
    }
}
