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
}
