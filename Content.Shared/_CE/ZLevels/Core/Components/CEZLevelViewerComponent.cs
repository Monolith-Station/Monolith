/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.ZLevels.Core.Components;

/// <summary>
/// Allows entity to see through Z-levels
/// </summary>
[UnsavedComponent]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(CESharedZLevelsSystem))]
public sealed partial class CEZLevelViewerComponent : Component
{
    public HashSet<EntityUid> Eyes = new();

    /// <summary>
    /// Whether to render visible levels above the viewer.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool LookUp;

    #region Actions

    [DataField]
    public EntProtoId ActionId = "CEActionToggleLookUp";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    #endregion
}
