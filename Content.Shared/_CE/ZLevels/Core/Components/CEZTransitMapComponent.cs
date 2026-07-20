/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._CE.ZLevels.Core.Components;

/// <summary>
/// PZN's finest: A map that holds a grid moving between Z levels. This components holds the neccesary state for it.
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
    /// The grid whose CEZPhysics LocalPosition defines this map's visual progress between the two levels.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? PrimaryGrid;
}
