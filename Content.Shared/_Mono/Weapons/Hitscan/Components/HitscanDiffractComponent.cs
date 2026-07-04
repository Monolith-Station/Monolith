using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Weapons.Hitscan.Components;

/// <summary>
/// Hitscan entities with this component will diffract when they hit entities with HitscanDiffractTargetComponent
/// Deletes original beam and spawns new beams with specified spread
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HitscanDiffractComponent : Component
{
    /// <summary>
    /// Number of new hitscan beams to spawn, 5 by default
    /// </summary>
    [DataField]
    public int BeamCount = 5;

    /// <summary>
    /// Angle spread between beams in radians
    /// </summary>
    [DataField]
    public float SpreadAngle = 0.5f;

    /// <summary>
    /// Prototype of the hitscan entity to spawn for each diffracted beam
    /// Leave as null to use parent hitscan proto for diffraction beams
    /// </summary>
    [DataField]
    public EntProtoId? DiffractedBeamPrototype;
}
