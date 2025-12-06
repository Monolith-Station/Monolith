using System.Numerics;
using System.Threading;
using Content.Server.NPC.Pathfinding;
using Content.Shared.DoAfter;
using Content.Shared.NPC;
using Robust.Shared.Map;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Mono.NPC.HTN;

/// <summary>
/// Added to entities that are steering their ship parent.
/// </summary>
[RegisterComponent]
public sealed partial class ShipSteererComponent : Component
{
    /// <summary>
    /// End target that we're trying to move to.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)] public EntityCoordinates Coordinates;

    /// <summary>
    /// How close are we trying to get to the coordinates before being considered in range.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)] public float Range = 5f;

    /// <summary>
    /// Up to how fast can we be going before being considered in range, if not null.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)] public float? InRangeMaxSpeed = null;

    /// <summary>
    /// Max rotation rate to be considered stationary.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)] public float MaxRotateRate = 0.01f;

    /// <summary>
    /// Whether to avoid collisions with other grids.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)] public bool AvoidCollisions = true;

    /// <summary>
    /// Target rotation in relation to movement direction.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)] public float TargetRotation = 0f;

    [ViewVariables] public ShipSteeringStatus Status = ShipSteeringStatus.Moving;
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
