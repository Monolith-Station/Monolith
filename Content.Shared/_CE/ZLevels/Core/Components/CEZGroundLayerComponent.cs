/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._CE.ZLevels.Core.Components;

/// <summary>
/// Marks a z-level map as a ground layer. Grids that land here are parked
/// (physics disabled, no piloting) until they leave for a sky layer, mirroring
/// how FTL arrivals to planets behave.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEZGroundLayerComponent : Component;
