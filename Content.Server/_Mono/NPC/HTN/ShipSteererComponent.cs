using Robust.Shared.Map;

namespace Content.Server._Mono.NPC.HTN;

/// <summary>
/// Added to entities that are steering their ship parent.
/// </summary>
[RegisterComponent]
public sealed partial class ShipSteererComponent : Component
{
    [ViewVariables]
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
    /// If AlwaysFaceTarget is true, how much of a difference in angle (in radians) to accept.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float AlwaysFaceTargetOffset = 0.01f;

    /// <summary>
    /// Whether to avoid obstacles.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool AvoidCollisions = true;

    /// <summary>
    /// How unwilling we are to use brake to adjust our velocity. Higher means less willing.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float BrakeThreshold = 0.75f;

    /// <summary>
    /// Whether to consider the movement finished if we collide with target.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool FinishOnCollide = true;

    /// <summary>
    /// Up to how fast can we be going before being considered in range, if not null.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float? InRangeMaxSpeed = null;

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
    /// Target rotation in relation to movement direction.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float TargetRotation = 0f;
}

public enum ShipSteeringStatus : byte
{
    /// <summary>
    /// Are we moving towards our target
    /// </summary>
    Moving,

    /// <summary>
    /// Are we currently in range of our target.
    /// </summary>
    InRange,
}

public enum ShipSteeringMode
{
    GoToRange,
    Orbit
}
