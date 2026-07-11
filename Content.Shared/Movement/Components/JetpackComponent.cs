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
    /// CE z-flight: top vertical speed (z-levels/second) the jetpack drives toward while the
    /// shuttle ascend/descend keys are held. Only relevant on z-level maps.
    /// </summary>
    [DataField]
    public float FlightMaxSpeed = 3f;

    /// <summary>
    /// CE z-flight: how sharply vertical velocity chases the ascend/descend input — higher
    /// reaches top speed (and coasts back to a hover on release) faster.
    /// </summary>
    [DataField]
    public float FlightResponsiveness = 8f;

    /// <summary>
    /// CE z-flight: with no ascend/descend held, how strongly the wearer drifts toward the
    /// nearest whole level plane (its floor from the lower half, the level above from the upper
    /// half), like a transit set settling onto a level. The pull scales with distance to the
    /// plane, so it eases to nothing right at it. 0 disables the settle — the wearer just hovers
    /// wherever they let go.
    /// </summary>
    [DataField]
    public float FlightSettleGain = 1f;
}
