using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Sensors;

[Serializable, NetSerializable]
public sealed class RadarLockStateDto
{
    public bool LockValid;
    public NetEntity? LockedTarget;
    public Vector2? TargetWorldPosition;
    public Vector2? AverageLeadWorld;
    public List<RadarWeaponLeadDto>? WeaponLeads;
    public bool RadarTrackEnabled;
    public bool Chaffed;
}

[Serializable, NetSerializable]
public sealed class RadarWeaponLeadDto
{
    public NetEntity Weapon;
    public float ProjectileSpeed;
    public Vector2 LeadWorld;
}

[Serializable, NetSerializable]
public sealed class SensorsSetLockMessage : BoundUserInterfaceMessage
{
    public NetEntity? Target;
}

[Serializable, NetSerializable]
public sealed class SensorsSetRadarTrackMessage : BoundUserInterfaceMessage
{
    public bool Enabled;
}

public readonly record struct RadarLockAcquiredEvent(EntityUid Console, EntityUid TargetGrid);
