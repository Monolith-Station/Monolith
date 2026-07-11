using Robust.Shared.GameStates;

namespace Content.Shared.Movement.Components;

/// <summary>
/// Added to someone using a jetpack for movement purposes
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class JetpackUserComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Jetpack;

    [DataField, AutoNetworkedField]
    public float WeightlessAcceleration;

    [DataField, AutoNetworkedField]
    public float WeightlessFriction;

    [DataField, AutoNetworkedField]
    public float WeightlessFrictionNoInput;

    [DataField, AutoNetworkedField]
    public float WeightlessModifier;

    /// <summary>CE z-flight: shuttle-ascend key held — drives the wearer up through the levels.
    /// Networked so the client-simulated z-physics integrates the same vertical thrust the
    /// server does; without it the predicted climb fights the server's and jitters.</summary>
    [ViewVariables, AutoNetworkedField]
    public bool AscendHeld;

    /// <summary>CE z-flight: shuttle-descend key held — drives the wearer down. Networked, see above.</summary>
    [ViewVariables, AutoNetworkedField]
    public bool DescendHeld;

    /// <summary>CE z-flight: the wearer's z-physics VelocityRaiseEvent flag before flight, restored on landing.</summary>
    [ViewVariables]
    public bool PriorVelocityRaiseEvent;
}
