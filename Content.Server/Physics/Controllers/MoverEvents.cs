using System.Numerics;

// Mono - whole file

namespace Content.Server.Physics.Controllers;

public record struct ShuttleInput(Vector2 Strafe, float Rotation, float Brakes);

/// <summary>
///     Raised on pilots to get inputs given to a shuttle.
///     If GotInput is false, this piloted is removed from input sources.
/// </summary>
[ByRefEvent]
public record struct GetShuttleInputsEvent(ShuttleInput? Input = null, bool GotInput = false);
