using System.Numerics;
using Content.Shared._Mono.Detection;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Sensors;

[Serializable, NetSerializable]
public enum SensorsUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class SensorsBuiState : BoundUserInterfaceState
{
    public List<SensorContactDto> Contacts = new();
    public List<SensorHardwareDto> Hardware = new();
    public List<string> DatalinkCodes = new();
}

[Serializable, NetSerializable]
public sealed class SensorContactDto
{
    public NetEntity Target;
    public string? Name;
    public float Range;
    public float BearingDegrees;
    public string Sources = string.Empty;
    public DetectionLevel Level;
    public float Rcs;
    public float IrSignature;
    public float ActiveRadarWatts;
}

[Serializable, NetSerializable]
public sealed class SensorHardwareDto
{
    public NetEntity Entity;
    public string Name = string.Empty;
    public SensorType SensorType;
    public bool Enabled;
    public bool ActiveMode;
    public bool Powered;
    public float Sensitivity;
    public float ArcDegrees;
    public float ArcOffset;
    public float TransmitWatts;
}

/// <summary>EMCON: enable/standby and active emit for one sensor.</summary>
[Serializable, NetSerializable]
public sealed class SensorsSetEmconMessage : BoundUserInterfaceMessage
{
    public NetEntity Sensor;
    public bool Enabled;
    public bool ActiveMode;
}

/// <summary>Steer jammer / directed sensor cone (degrees, world-offset from entity facing).</summary>
[Serializable, NetSerializable]
public sealed class SensorsSetArcMessage : BoundUserInterfaceMessage
{
    public NetEntity Sensor;
    public float ArcOffsetDegrees;
}

[Serializable, NetSerializable]
public sealed class SensorsSetDatalinkMessage : BoundUserInterfaceMessage
{
    public List<string> Codes = new();
}

[Serializable, NetSerializable]
public sealed class SensorsToggleHardwareMessage : BoundUserInterfaceMessage
{
    public NetEntity Sensor;
    public bool? Enabled;
    public bool? ActiveMode;
}
