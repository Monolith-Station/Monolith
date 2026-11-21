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
    /// Keep it on +90 default, 0 degrees is east instead of north.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ArcOffset = 90f;

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
    public bool Enabled = true;

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
