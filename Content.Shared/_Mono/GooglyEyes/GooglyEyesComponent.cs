using Robust.Shared.GameStates;
using System.Numerics;

namespace Content.Shared._Mono.GooglyEyes;

[RegisterComponent, NetworkedComponent]
public sealed partial class GooglyEyesComponent : Component
{
    /// <summary>
    /// Current world-relative eye velocity.
    /// </summary>
    [DataField]
    public Vector2 Velocity = Vector2.Zero;

    /// <summary>
    /// Current eye offset in world coordinates.
    /// </summary>
    [DataField]
    public Vector2 Coordinates = Vector2.Zero;

    [DataField]
    public string Layer = "eye";

    /// <summary>
    /// Maximum radius the eyes can go to.
    /// </summary>
    [DataField]
    public float Radius = 0.125f;

    /// <summary>
    /// Friction coefficient to apply to the eyes.
    /// 0.2 means that at a velocity of 1 we will slow down at 0.2/s^2.
    /// </summary>
    [DataField]
    public float Friction = 0.2f;

    /// <summary>
    /// How much to bounce when colliding with an edge.
    /// </summary>
    [DataField]
    public float Bounciness = 0.2f;

    /// <summary>
    /// Chance per second that the eye gets a clientside nudge to the current player.
    /// </summary>
    [DataField]
    public float LookProb = 0.02f;

    /// <summary>
    /// Maximum eye velocity for look to trigger.
    /// </summary>
    [DataField]
    public float LookMaxVelocity = 0.002f;
}
