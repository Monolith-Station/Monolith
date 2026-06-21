using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared.ArtilleryDetection.Components;

/// <summary>
/// Component for an artillery fire detection system.
/// Detects nearby weapon fire and logs the events.
/// </summary>
[RegisterComponent]
public sealed partial class ArtilleryDetectorComponent : Component
{
    /// <summary>
    /// Detection radius in tiles.
    /// </summary>
    [DataField]
    public float DetectionRadius = 100f;

    /// <summary>
    /// Time delay before fire is logged, in seconds.
    /// </summary>
    [DataField]
    public float DetectionDelay = 2f;

    /// <summary>
    /// Detection accuracy in tiles on X axis.
    /// The actual coordinates reported will be off by this amount at most.
    /// </summary>
    [DataField]
    public float AccuracyX = 2f;

    /// <summary>
    /// Detection accuracy in tiles on Y axis.
    /// The actual coordinates reported will be off by this amount at most.
    /// </summary>
    [DataField]
    public float AccuracyY = 2f;

    /// <summary>
    /// Whether the detector should display the type of artillery unit in the console.
    /// </summary>
    [DataField]
    public bool ShowArtilleryType = true;

    /// <summary>
    /// Whether the detector should display the type of projectile/shell in the console.
    /// </summary>
    [DataField]
    public bool ShowProjectileType = true;
}

