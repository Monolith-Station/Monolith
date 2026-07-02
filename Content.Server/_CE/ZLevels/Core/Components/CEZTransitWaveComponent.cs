/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

namespace Content.Server._CE.ZLevels.Core.Components;

/// <summary>
/// Debug: bobs a transiting grid up and down on a sine wave around a center altitude.
/// Removes itself when the grid leaves transit (e.g. by landing).
/// </summary>
[RegisterComponent]
public sealed partial class CEZTransitWaveComponent : Component
{
    /// <summary>
    /// Absolute z-network altitude the wave oscillates around.
    /// </summary>
    [DataField]
    public float CenterAltitude;

    /// <summary>
    /// Peak offset above/below the center, in levels.
    /// </summary>
    [DataField]
    public float Amplitude = 1f;

    /// <summary>
    /// Seconds for one full up-and-down cycle.
    /// </summary>
    [DataField]
    public float Period = 10f;

    [DataField]
    public TimeSpan StartTime;
}
