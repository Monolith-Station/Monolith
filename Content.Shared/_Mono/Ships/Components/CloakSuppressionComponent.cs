using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Ships.Components;

/// <summary>
/// Component that tracks ships whose IFF Hide flag has been suppressed by nearby CloakHunter ships.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]

public sealed partial class CloakSuppressionComponent : Component
{
    /// <summary>
    /// The EntityUid of the CloakHunter ship that is suppressing this ship's IFF.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SuppressingShip;

    /// <summary>
    /// Timestamp when the suppression started.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan SuppressionStartTime;

    /// <summary>
    /// The original ReadOnly state of the IFF component before suppression.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool OriginalReadOnlyState;
}
