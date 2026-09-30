/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

namespace Content.Shared._CE.ZLevels.Core.Components;

/// <summary>
/// When anchored, links this entity's parent grid to the grid on the z-level directly above,
/// provided a tile exists at this position on that upper grid.
/// Multiple connector entities can independently maintain the same grid pair.
/// </summary>
[RegisterComponent]
public sealed partial class CEZGridConnectorComponent : Component
{
}
