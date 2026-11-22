using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Sensors;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipSensorComponent : Component
{
    [DataField("sensorType"), AutoNetworkedField]
    public SensorType SensorType = SensorType.PassiveRadar;

    /// <summary>
    /// Multiplier for relevant detection range.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Sensitivity = 1f;

    /// <summary>
    /// Self explanatory.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ArcDegrees = 360f;

    /// <summary>
    /// Offset from the ent rotation for arc
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ArcOffset;

    /// <summary>
    /// If we can direct the arc at all.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ArcConfigurable = true;

    /// <summary>
    /// If our arcs are stabilized or not.
    /// If it's true, they rotate with the ship.
    /// if it's false, it's stabilized and always faces it's directed bearing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool GridRelativeArc;

    /// <summary>
    /// Power draw when disabled
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PowerDraw = 50f;

    [DataField, AutoNetworkedField]
    public float PowerDrawActive = 200f;

    /// <summary>
    /// Thermal sig added when jammer/radar active
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ActiveThermalSignature;

    [DataField, AutoNetworkedField]
    public bool Enabled;

    /// <summary>
    /// if radars are in passive/active, or if jammers are on/off
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ActiveMode;

    [DataField, AutoNetworkedField]
    public bool RequireLineOfSight;
}

[Serializable, NetSerializable]
public enum SensorType : byte
{
    PassiveRadar = 0,
    ActiveRadar = 1,
    Infrared = 2
}
