using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
[Virtual]
public class RadarConsoleBoundInterfaceState : BoundUserInterfaceState
{
    public NavInterfaceState NavState;
    public readonly DockingInterfaceState DockState; //todo (radars): docks are only required for the shuttle console; move em outta here
    public readonly List<CommonRadarEntityInterfaceState> CommonEntities;

    //todo (radars): we are already sending all the data we need for the radar's UI, by dirtying cannons, shields, and other stuff,
    //yet we redundantly send those BUI states. we need to come up with a way to separate shield, cannon and shuttle console windows
    //functionality into something like radar modules, and force them to use clients comp data
    //...or atleast remove docks from this state and move it to shuttle console
    public RadarConsoleBoundInterfaceState(
        NavInterfaceState navState,
        DockingInterfaceState dockState,
        List<CommonRadarEntityInterfaceState> common)
    {
        NavState = navState;
        DockState = dockState;
        CommonEntities = common;
    }
}

[Serializable, NetSerializable]
public sealed class CommonRadarEntityInterfaceState
{
    public NetCoordinates Coordinates;
    public Angle Angle;
    public List<string> ViewPrototypes;
    public Color? OverrideColor;

    public CommonRadarEntityInterfaceState(NetCoordinates coordinates, Angle angle, List<string> viewPrototypes,
        Color? color = null)
    {
        Coordinates = coordinates;
        Angle = angle;
        ViewPrototypes = viewPrototypes;
        OverrideColor = color;
    }
}

[Flags]
[Serializable, NetSerializable]
public enum RadarRenderableGroup
{
    None = 0,
    ShipEventTeammate = 1 << 0,
    Projectiles = 1 << 1,
    Cannon = 1 << 2,
    Door = 1 << 3,
    Pickup = 1 << 4,
    Anomaly = 1 << 5,

    All = (ShipEventTeammate | Projectiles | Cannon | Door | Pickup | Anomaly),
}

[Serializable, NetSerializable]
public sealed class ShieldInterfaceState
{
    public NetCoordinates Coordinates;
    public bool Powered;
    public bool Enabled;
    public Angle Angle;
    public Angle Width;
    public Angle MaxWidth;
    public int Radius;
    public int MaxRadius;
    public int PowerDraw;
}

/// <summary>
/// State of each door on shuttle grid
/// </summary>
[Serializable, NetSerializable]
public sealed class DoorInterfaceState
{
    public NetEntity Uid;
}
