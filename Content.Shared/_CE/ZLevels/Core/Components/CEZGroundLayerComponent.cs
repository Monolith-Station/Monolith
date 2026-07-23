/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._CE.ZLevels.Core.Components;

/// <summary>
/// Marks a z-level map as a ground layer. Purely presentational: it tells the client not to
/// render or peer past this level, and labels the shuttle console's flight status. Whether a
/// ship can rest on, land on, or climb through a level is decided by that level's terrain
/// tiles instead (see CEZLevelsSystem.HasGroundUnderFootprint), so a layer does not need this
/// component to be solid, and having it does not make an empty layer solid.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEZGroundLayerComponent : Component;
