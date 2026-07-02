/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

namespace Content.Server._CE.ZLevels.Core.Components;

/// <summary>
/// Server-side gravity state for a grid on a z-network. Added when a grid arrives on
/// a z-level (with a grace period before gravity arms), removed when it leaves.
/// Fall velocity lives here, not in CEZPhysics: grids are integrated by the server
/// transit loop, never by the shared per-entity z-physics loop.
/// </summary>
[RegisterComponent]
public sealed partial class CEZGridFallerComponent : Component
{
    /// <summary>
    /// Gravity does not act before this time (grace period after entering z-levels).
    /// </summary>
    [DataField]
    public TimeSpan GravityTime;

    /// <summary>
    /// Current fall speed in levels per second. Positive = downward.
    /// </summary>
    [DataField]
    public float Velocity;
}
