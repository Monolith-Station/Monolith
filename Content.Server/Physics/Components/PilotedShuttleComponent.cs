using System.Numerics;

// Mono - whole file

namespace Content.Server.Physics.Controllers;

/// <summary>
///     Component used to store entities that want to give input to this shuttle.
/// </summary>
[RegisterComponent]
public sealed partial class PilotedShuttleComponent : Component
{
    /// <summary>
    ///     List of sources to query for input for this shuttle.
    ///     Cleaned up automatically if the entity did not respond to GetShuttleInputsEvent.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> InputSources = new();
}
