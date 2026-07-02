/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._CE.ZLevels.Core.Components;

/// <summary>
/// Marks a z-level map as a ground layer. You can't fly a ship on the ground, dummy.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEZGroundLayerComponent : Component;
