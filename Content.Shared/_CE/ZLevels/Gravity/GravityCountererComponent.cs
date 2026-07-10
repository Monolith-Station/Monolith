/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.ZLevels.Gravity;

/// <summary>
/// Component for entity that counteracts planetary gravity (allows a grid to stay floating in the air)
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GravityCountererComponent : Component
{
    // How much mass the generator can take.
    [DataField, AutoNetworkedField]
    public float MassCapacity;

    // The amount of power the generator takes at maximum load.
    [DataField, AutoNetworkedField]
    public string MaxPowerUsage;

    // The amount of power the generator takes at idle.
    [DataField, AutoNetworkedField]
    public string MinPowerUsage;
}
