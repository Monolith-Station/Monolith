using Robust.Shared.Map;
using System.Numerics;

namespace Content.Server._Mono.NPC.HTN;

/// <summary>
/// Added to entities that are steering their ship parent.
/// </summary>
[RegisterComponent]
public sealed partial class ShipSteererComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public ShipSteeringStatus Status = ShipSteeringStatus.Moving;

    /// <summary>
    /// End target that we're trying to move to.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public EntityCoordinates Coordinates;

    /// <summary>
    /// Whether to keep facing target if backing off due to RangeTolerance.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool AlwaysFaceTarget = false;

    /// <summary>
    /// Whether to avoid shipgun projectiles.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool AvoidProjectiles = false;

    /// <summary>
    /// Prevents collision avoidance from triggering ship rotation.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool AvoidanceNoRotate = false;

    /// <summary>
    /// If AlwaysFaceTarget is true or InRangeRotation is set, how much of a difference in angle (in radians) to accept.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float RotationTolerance = 0.0333f;

    /// <summary>
    /// Whether to avoid obstacles.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool AvoidCollisions = true;

    /// <summary>
    /// Try to evade collisions this far into the future even if stationary.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float BaseEvasionTime = 4f;

    /// <summary>
    /// Don't use anchor below this velocity.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float AnchorMaxVelocity = 5f;

    /// <summary>
    /// How unwilling we are to use brake to adjust our velocity. Higher means less willing.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float BrakeThreshold = 0.3f;

    /// <summary>
    /// How much larger to consider the ship for collision evasion purposes.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float EvasionBuffer = 3f;

    /// <summary>
    /// How many evasion sectors to init on the outer ring.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public int EvasionSectorCount = 24;

    /// <summary>
    /// How many layers of evasion sectors to have.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public int EvasionSectorDepth = 2;

    /// <summary>
    /// Whether to consider the movement finished if we collide with target.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool FinishOnCollide = true;

    /// <summary>
    /// How much to enlarge grid search bounds for collision evasion.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float GridSearchBuffer = 312f;

    /// <summary>
    /// How much to enlarge grid search forward distance for collision evasion.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float GridSearchDistanceBuffer = 96f;

    /// <summary>
    /// Up to how fast can we be going before being considered in range, if not null.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float? InRangeMaxSpeed = null;

    /// <summary>
    /// Global angle to rotate to while in range.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public Angle? InRangeRotation = null;

    /// <summary>
    /// Whether to try to match velocity with target.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool LeadingEnabled = true;

    /// <summary>
    /// Max rotation rate to be considered stationary, if not null.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float? MaxRotateRate = null;

    /// <summary>
    /// Check for obstacles for collision avoidance at most this far.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float MaxObstructorDistance = 800f;

    /// <summary>
    /// Ignore obstacles this close to our destination grid if moving to a grid, + other grid's radius.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float MinObstructorDistance = 20f;

    /// <summary>
    /// Don't finish early even if we've completed our order.
    /// Use to keep doing collision detection when we're supposed to finish on plan finish.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool NoFinish = false;

    /// <summary>
    /// What movement behavior to use.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public ShipSteeringMode Mode = ShipSteeringMode.GoToRange;

    /// <summary>
    /// How much to angularly offset our movement target on orbit movement mode.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public Angle OrbitOffset = Angle.FromDegrees(30f);

    /// <summary>
    /// In what radius to search for projectiles in for collision evasion.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float ProjectileSearchBounds = 896f;

    /// <summary>
    /// How close are we trying to get to the coordinates before being considered in range.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float Range = 5f;

    /// <summary>
    /// At most how far to stay from the desired range. If null, will consider the movement finished while in range.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float? RangeTolerance = null;

    /// <summary>
    /// Accumulator for an integral of our rotational offset to target.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float RotationCompensation = 0f;

    /// <summary>
    /// How fast to accumulate the rotational offset integral, rad/s/rad (also affected by sqrt of angular acceleration).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float RotationCompensationGain = 0.1f;

    /// <summary>
    /// Target rotation in relation to movement direction.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float TargetRotation = 0f;
}

public enum ShipSteeringStatus : byte
{
    /// <summary>
    /// Moving towards target
    /// </summary>
    Moving,

    /// <summary>
    /// Meeting set end conditions
    /// </summary>
    InRange
}

public enum ShipSteeringMode
{
    GoToRange,
    Orbit,
    OrbitCW
}
