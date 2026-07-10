/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._CE.ZLevels.Core.Components;

/// <summary>
/// A map holding grids that are vertically between two z-levels (falling or hovering
/// ships). Grids here are rendered by the client as an extra viewport pass at a
/// fractional depth: the primary grid's CEZPhysics LocalPosition is the progress
/// through the gap, 0 = at <see cref="LowerMap"/>'s plane, 1 = at <see cref="UpperMap"/>'s.
/// One transit map exists per moving grid-set and is deleted when the set leaves.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEZTransitMapComponent : Component
{
    /// <summary>
    /// The z-level below the gap this map occupies.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? LowerMap;

    /// <summary>
    /// The z-level above the gap this map occupies.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? UpperMap;

    /// <summary>
    /// The grid whose CEZPhysics LocalPosition defines this map's visual progress
    /// between the two levels (docked companions follow it).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? PrimaryGrid;

    // ===== Convoy: a z-grid network in transit spans one transit map per layer, =====
    // ===== vertically linked so the stack moves (and can be traversed) as one.  =====

    /// <summary>
    /// Transit map of the convoy layer directly above this one, if this map is part
    /// of a z-network convoy. Together with <see cref="TransitBelow"/> this forms a
    /// temporary z-stack, letting connector recalculation and (later) z-movement
    /// treat the convoy like ordinary stacked levels.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? TransitAbove;

    /// <summary>
    /// Transit map of the convoy layer directly below this one, if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? TransitBelow;

    /// <summary>
    /// True on the single convoy layer that drives gravity/pilot integration for the
    /// whole stack (the layer whose grid initiated transit). Follower layers mirror
    /// its velocity instead of integrating themselves. Meaningless outside convoys.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ConvoyLead;
}
