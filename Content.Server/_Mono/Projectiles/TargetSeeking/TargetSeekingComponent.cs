namespace Content.Server._Mono.Projectiles.TargetSeeking;

/// <summary>
/// Component that allows a projectile to seek and track targets autonomously.
/// </summary>
[RegisterComponent]
public sealed partial class TargetSeekingComponent : Component
{
    /// <summary>
    /// Maximum distance to search for potential targets.
    /// </summary>
    [DataField]
    public float DetectionRange = 300f;

    /// <summary>
    /// Minimum angular deviation from directly facing the target.
    /// </summary>
    [DataField]
    public Angle Tolerance = Angle.FromDegrees(1);

    /// <summary>
    /// How quickly the projectile can change direction in degrees per second.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Angle? TurnRate = 100f;

    /// <summary>
    /// The current target entity being tracked.
    /// </summary>
    [DataField]
    public EntityUid? CurrentTarget;

    /// <summary>
    /// Should tracked entities know that they are being tracked?
    /// </summary>
    [DataField]
    public bool ExposesTracking = true;

    /// <summary>
    /// Tracking algorithm used for intercepting the target.
    /// </summary>
    [DataField]
    public TrackingMethod TrackingAlgorithm = TrackingMethod.AdvancedPredictive;

    /// <summary>
    /// How fast the projectile accelerates in m/s².
    /// </summary>
    [DataField]
    public float Acceleration = 50f;

    /// <summary>
    /// Maximum speed the projectile can reach in m/s.
    /// </summary>
    [DataField]
    public float MaxSpeed = 50f;

    /// <summary>
    /// Initial speed of the projectile in m/s.
    /// </summary>
    [DataField]
    public float LaunchSpeed = 10f;

    /// <summary>
    /// Has the projectile had its launch speed applied yet?
    /// </summary>
    [DataField]
    public bool Launched = false;

    /// <summary>
    /// The amount of time in seconds left the missile starts searching for targets. // Mono
    /// </summary>
    [DataField]
    public float TrackDelay = 0f;

    /// <summary>
    /// Field of view in degrees for target detection.
    /// </summary>
    [DataField]
    public float ScanArc = 90f;

    /// <summary>
    /// Whether seeking has been disabled (e.g., after entering an enemy grid).
    /// </summary>
    public bool SeekingDisabled;
}

/// <summary>
/// Defines different tracking algorithms that can be used.
/// </summary>
[Serializable]
public enum TrackingMethod
{
    /// <summary>
    /// Basic tracking that simply points directly at the target.
    /// </summary>
    Direct = 1,

    /// <summary>
    /// Advanced tracking that predicts target movement.
    /// </summary>
    Predictive = 2,

    /// <summary>
    /// Even more accurate tracking.
    /// </summary>
    AdvancedPredictive = 3
}
