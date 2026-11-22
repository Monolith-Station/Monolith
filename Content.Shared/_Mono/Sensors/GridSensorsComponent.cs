using System.Numerics;
using Content.Shared._Mono.Detection;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Sensors;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GridSensorsComponent : Component
{
    /// <summary>
    /// if we give the grid basic little baby shit sensors
    /// </summary>
    [DataField]
    public bool GrantBasicSensors = true;

    [DataField]
    public float BasicVisualMultiplier = 1f;

    [DataField]
    public float BasicInfraredMultiplier = 0.2f;

    [DataField]
    public float BasicPassiveRadarMultiplier = 0.2f;

    /// <summary>
    /// time since last detect that contacts remain for
    /// </summary>
    [DataField]
    public TimeSpan ContactStaleTime = TimeSpan.FromSeconds(8);

    /// <summary>
    /// contacts detected excluding datalink, since like, feedback loop
    /// </summary>
    [NonSerialized]
    public HashSet<NetEntity> LocallySensed = new();

    [DataField, AutoNetworkedField]
    public HashSet<string> DatalinkCodes = new();

    [ViewVariables, AutoNetworkedField]
    public Dictionary<NetEntity, SensorContact> Contacts = new();
}

[DataRecord, Serializable, NetSerializable]
public partial record struct SensorContact
{
    public NetEntity TargetGrid;
    public Vector2 WorldPosition;
    public float Range;
    public DetectionLevel Level;
    public SensorContactFlags Flags;
    public TimeSpan LastSeen;
    public string? Designation;
}

[Flags, Serializable, NetSerializable]
public enum SensorContactFlags : byte
{
    None = 0,
    Visual = 1 << 0,
    Infrared = 1 << 1,
    PassiveRadar = 1 << 2,
    ActiveRadar = 1 << 3,
    Datalink = 1 << 4,
}
