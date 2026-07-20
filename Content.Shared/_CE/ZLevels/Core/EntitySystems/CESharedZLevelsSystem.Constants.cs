/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    public static int MaxZLevelsBelowRendering = 10;
    public const float ZLevelOffset = 0.7f;

    /// <summary>
    /// Per-level perspective factor: each z-level below the observer is drawn this
    /// much smaller (and each level above this much larger), continuously with depth.
    /// </summary>
    public const float ZLevelViewShrink = 0.85f;

    public const float ZGravityForce = 9.8f;
    private const float ZVelocityLimit = 20.0f;

    /// <summary>
    /// The minimum speed required to trigger LandEvent events.
    /// </summary>
    private const float ImpactVelocityLimit = 3.5f;

    /// <summary>
    /// Distance to ground above which an entity is considered airborne (synced to BodyStatus.InAir).
    /// </summary>
    public const float AirborneHeightThreshold = 0.15f;
}
