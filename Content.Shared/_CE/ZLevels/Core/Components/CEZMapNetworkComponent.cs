/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.ZLevels.Core.Components;

/// <summary>
/// Tracker that tracks all maps added to the zLevel network. Usually, entity in Nullspace,
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CESharedZLevelsSystem))]
public sealed partial class CEZMapNetworkComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public readonly Dictionary<int, EntityUid?> ZLevels = new();

    /// <summary>
    /// Shared components for all zLevels maps
    /// </summary>
    [DataField(serverOnly: true)]
    public ComponentRegistry Components = new();

    /// <remarks>
    /// Reversed version of <see cref="ZLevels"/>
    /// </remarks>
    [ViewVariables, AutoNetworkedField]
    public readonly Dictionary<EntityUid, int> ZLevelByEntity = new();

    [ViewVariables, AutoNetworkedField]
    public readonly List<EntityUid> SortedZLevels = new();

    [ViewVariables, AutoNetworkedField]
    public int SortedMin = 0;

    [ViewVariables, AutoNetworkedField]
    public int SortedMax = 0;
}
