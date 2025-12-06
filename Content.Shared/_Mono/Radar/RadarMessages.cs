using System.Linq;
using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Radar;

[Serializable, NetSerializable]
public enum RadarBlipShape
{
    Circle,
    Square,
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
    /// Blips are now (position, velocity, scale, color, shape).
    /// </summary>
    public readonly List<(NetEntity uid, NetCoordinates Position, Vector2 Vel, float Scale, Color Color, RadarBlipShape Shape)> Blips;

    /// <summary>
    /// Hitscan lines to display on the radar as (start position, end position, thickness, color).
    /// </summary>
    public readonly List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> HitscanLines;

    public GiveBlipsEvent(List<(NetEntity uid, NetCoordinates Position, Vector2 Vel, float Scale, Color Color, RadarBlipShape Shape)> blips)
    {
        Blips = blips;
        HitscanLines = new List<(Vector2 Start, Vector2 End, float Thickness, Color Color)>();
    }

    public GiveBlipsEvent(
        List<(NetEntity uid, NetCoordinates Position, Vector2 Vel, float Scale, Color Color, RadarBlipShape Shape)> blips,
        List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> hitscans)
    {
        Blips = blips;
        HitscanLines = hitscans;
    }
}

[Serializable, NetSerializable]
public sealed class RequestBlipsEvent : EntityEventArgs
{
    public NetEntity Radar;
    public RequestBlipsEvent(NetEntity radar)
    {
        Radar = radar;
    }
}

[Serializable, NetSerializable]
public sealed class BlipRemovalEvent : EntityEventArgs
{
    public NetEntity NetBlipUid { get; set; }

    public BlipRemovalEvent(NetEntity netBlipUid)
    {
        NetBlipUid = netBlipUid;
    }
}