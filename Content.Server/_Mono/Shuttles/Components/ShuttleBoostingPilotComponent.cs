namespace Content.Server._Mono.Shuttles.Components;

/// <summary>
/// Modifies how shuttles piloted by this entity drive.
/// </summary>
[RegisterComponent]
public sealed partial class ShuttleBoostingPilotComponent : Component
{
    [DataField]
    public float AngularMultiplier = 1f;

    [DataField]
    public float AccelerationMultiplier = 1f;

    [DataField]
    public float MaxVelMultiplier = 1f;
}
