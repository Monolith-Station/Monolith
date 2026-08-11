using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Content.Shared._Mono.Radar;

namespace Content.Shared.Movement.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class JetpackComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? JetpackUser;

    [ViewVariables(VVAccess.ReadWrite), DataField("moleUsage")]
    public float MoleUsage = 0.012f;

    [ViewVariables(VVAccess.ReadWrite), DataField("verticalUsageMult")]
    public float UpwardsMultiplier = 5f; // 5x as expensive to go upwards; beware!!

    [DataField] public EntProtoId ToggleAction = "ActionToggleJetpack";

    [DataField, AutoNetworkedField] public EntityUid? ToggleActionEntity;

    [ViewVariables(VVAccess.ReadWrite), DataField("acceleration")]
    public float Acceleration = 1f;

    [ViewVariables(VVAccess.ReadWrite), DataField("friction")]
    public float Friction = 0.25f; // same as off-grid friction

    [ViewVariables(VVAccess.ReadWrite), DataField("weightlessModifier")]
    public float WeightlessModifier = 1.2f;

    /// <summary>
    /// Mono - Determines the range that a jetpack shows up on blip radar.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public float DetectionRange = 256f;

    /// <summary>
    /// CEZ: Maximum heave velocity.
    /// </summary>
    [DataField]
    public float FlightMaxSpeed = 1f;

    /// <summary>
    /// CEZ: Acceleration. Controls how fast upwards/downwards movement is.
    /// </summary>
    [DataField]
    public float FlightResponsiveness = 2f;

    /// <summary>
    /// CEZ: How quickly the user settles towards a normal layer when jetpacking.
    /// </summary>
    [DataField]
    public float FlightSettleGain = 2f;

    public bool IsZMoving = true;
}
