/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

namespace Content.Server._CE.ZLevels.Core.Components;

/// <summary>
/// Server-side gravity state for a grid on a z-network.
/// </summary>
[RegisterComponent]
public sealed partial class CEZGridFallerComponent : Component
{
    /// <summary>
    /// Grace period before you plummet violently.
    /// </summary>
    [DataField]
    public TimeSpan GravityTime;

    /// <summary>
    /// Current fall speed (levels per second)
    /// </summary>
    [DataField]
    public float Velocity;

    /// <summary>
    /// Which way the grid is starting to move initially. Probably going to be removed in later reworks.
    /// </summary>
    [DataField]
    public sbyte SpoolDirection;

    /// <summary>
    /// Time when the spoolup start began.
    /// </summary>
    [DataField]
    public TimeSpan SpoolStart;

    /// <summary>
    /// Last time the key was held because gridmovement is unfortunately serversided. Gurgh.
    /// </summary>
    [DataField]
    public TimeSpan SpoolLastInput;

    // Gridgrav values (Originally in GridMovement)

    /// <summary>
    /// Seconds after arriving on a z-network before gravity acts on a grid.
    /// </summary>
    [DataField]
    public float GridGravityGraceSeconds = 3f;

    /// <summary>
    /// Downward acceleration in levels per second squared. Falls start slow and build.
    /// </summary>
    [DataField]
    public float GridGravity = 0.15f;

    /// <summary>
    /// Maximum fall speed in levels per second.
    /// </summary>
    [DataField]
    public float GridTerminalVelocity = 1.2f;

    /// <summary>
    /// Touchdown speed at or above which a ground-layer landing is a crash.
    /// A full one-gap free fall arrives at ~0.74.
    /// </summary>
    [DataField]
    public float GridCrashVelocity = 0.35f;

    /// <summary>
    /// Roughly a 3x3 crater per hull tile on crash.
    /// </summary>
    [DataField]
    public float CrashTileIntensity = 12f;

    [DataField]
    public float CrashTileSlope = 2f;

    [DataField]
    public float CrashTileMaxIntensity = 4f;

    /// <summary>
    /// The central crash blast scales with hull size.
    /// </summary>
    [DataField]
    public float CrashIntensityPerTile = 5f;

    [DataField]
    public float CrashCenterSlope = 5f;

    [DataField]
    public float CrashCenterMaxIntensity = 100f;
}
