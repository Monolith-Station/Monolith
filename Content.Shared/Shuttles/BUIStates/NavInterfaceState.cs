using Robust.Shared.Map;
using Robust.Shared.Serialization;
using System.Numerics;
using Content.Shared._NF.Shuttles.Events; // Frontier - InertiaDampeningMode access
using Content.Shared.Shuttles.Components; // Frontier

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class NavInterfaceState
{
    public float MaxRange;

    /// <summary>
    /// The relevant coordinates to base the radar around.
    /// </summary>
    public NetCoordinates? Coordinates;

    /// <summary>
    /// The relevant rotation to rotate the angle around.
    /// </summary>
    public Angle? Angle;

    public Dictionary<NetEntity, List<DockingPortState>> Docks;

    /// <summary>
    /// Custom display names for network port buttons.
    /// Key is the port ID, value is the display name.
    /// </summary>
    public Dictionary<string, string> NetworkPortNames = new();

    // Frontier fields
    /// <summary>
    /// Frontier - the state of the shuttle's inertial dampeners
    /// </summary>
    public InertiaDampeningMode DampeningMode;

    /// <summary>
    /// Frontier: settable maximum IFF range
    /// </summary>
    public float? MaxIffRange = null;

    /// <summary>
    /// Frontier: settable coordinate visibility
    /// </summary>
    public bool HideCoords = false;

    /// <summary>
    /// A settable target to show on radar
    /// </summary>
    public Vector2? Target { get; set; }

    /// <summary>
    /// A settable target to show on radar
    /// </summary>
    public NetEntity? TargetEntity { get; set; }

    /// <summary>
    /// Frontier: whether or not to show the target coords
    /// </summary>
    public bool HideTarget = true;
    // End Frontier fields

    public NavInterfaceState(
        float maxRange,
        NetCoordinates? coordinates,
        Angle? angle,
        Dictionary<NetEntity, List<DockingPortState>> docks,
        InertiaDampeningMode dampeningMode, // Frontier
        Vector2? target, // Frontier
        NetEntity? targetEntity, // Frontier
        bool hideTarget, // Frontier
        Dictionary<string, string>? networkPortNames = null) // Mono
    {
        MaxRange = maxRange;
        Coordinates = coordinates;
        Angle = angle;
        Docks = docks;
        DampeningMode = dampeningMode; // Frontier
        Target = target; // Frontier
        TargetEntity = targetEntity; // Frontier
        HideTarget = hideTarget; // Frontier
        NetworkPortNames = networkPortNames ?? new Dictionary<string, string>(); // Mono
    }
}

[Serializable, NetSerializable]
public enum RadarConsoleUiKey : byte
{
    Key
}
