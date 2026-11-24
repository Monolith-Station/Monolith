using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Sensors;

[Serializable, NetSerializable]
public sealed class RadarLockStateDto
{
    public bool LockValid;
    public bool Coasting;
    public float Quality;
    public NetEntity? LockedTarget;
    public Vector2? TargetWorldPosition;
    public Vector2? TargetWorldVelocity;
    public Vector2? AverageLeadWorld;
    public float? TimeToIntercept;
    public List<RadarWeaponLeadDto>? WeaponLeads;
    public bool RadarTrackEnabled;
    public bool Chaffed;
    public TimeSpan ServerTime;
}

[Serializable, NetSerializable]
public sealed class RadarWeaponLeadDto
{
    public NetEntity Weapon;
    public float ProjectileSpeed;
    public Vector2 LeadWorld;
    public float TimeToIntercept;
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
