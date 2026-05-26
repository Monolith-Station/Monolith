using System.Numerics;

// Mono - whole file

namespace Content.Server.Physics.Controllers;

public record struct ShuttleInput(Vector2 Strafe, float Rotation, float Brakes);

/// <summary>
///     Raised on pilots to get inputs given to a shuttle.
///     If GotInput is false, this piloted is removed from input sources.
///     Also queries for multipliers to acceleration and max speed.
/// </summary>
[ByRefEvent]
public record struct GetShuttleInputsEvent(float FrameTime, EntityUid ShuttleUid, ShuttleInput? Input = null, bool GotInput = false)
{
    public float AngularMul = 1f;
    public float AccelMul = 1f;
    public float? SetMaxVelocity = null;
}

[ByRefEvent]
public record struct PilotedShuttleRelayedEvent<TEvent>(TEvent Args);
