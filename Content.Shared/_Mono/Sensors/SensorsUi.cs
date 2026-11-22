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
    public float GridWorldRotationDegrees;
}

[Serializable, NetSerializable]
public sealed class SensorContactDto
{
    public NetEntity Target;
    public string? Name;
    /// <summary>Server-computed range; kept so the client can still show contacts when the target is not in PVS.</summary>
    public float Range;
    /// <summary>Display bearing (same convention the UI already uses).</summary>
    public float BearingDegrees;
    public string Sources = string.Empty;
    public float Rcs;
    public float IrSignature;
    public float ActiveRadarWatts;
    public TimeSpan LastSeen;
    /// <summary>True only when the contact is datalink-only (no local ARS/E/IR/V).</summary>
    public bool IsDatalink;
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
    public bool ArcConfigurable;
    public bool GridRelativeArc;
}

[Serializable, NetSerializable]
public sealed class SensorsSetEmconMessage : BoundUserInterfaceMessage
{
    public NetEntity Sensor;
    public bool Enabled;
    public bool ActiveMode;
}

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
