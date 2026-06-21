using Robust.Shared.Map;
using Robust.Shared.Serialization;
using System;
using System.Numerics;

namespace Content.Shared.ArtilleryDetection;

/// <summary>
/// Represents a detected artillery fire event.
/// </summary>
[Serializable, NetSerializable]
public sealed class ArtilleryFireEvent
{
    /// <summary>
    /// Unique ID for this event.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Timestamp when the fire was detected.
    /// </summary>
    public TimeSpan DetectionTime { get; set; }

    /// <summary>
    /// Approximate coordinates of the shot (with inaccuracy applied).
    /// </summary>
    public Vector2 DetectedCoordinates { get; set; }

    /// <summary>
    /// Type of weapon that fired.
    /// </summary>
    public string WeaponType { get; set; } = "Unknown";

    /// <summary>
    /// Type of artillery unit (e.g., "1A103E 82mm mortar").
    /// </summary>
    public string ArtilleryType { get; set; } = "Unknown";

    /// <summary>
    /// Type of projectile/shell fired.
    /// </summary>
    public string ProjectileType { get; set; } = "Unknown";

    /// <summary>
    /// Local sequential ID for this event (per detector).
    /// </summary>
    public int LocalId { get; set; }

    public ArtilleryFireEvent() { }

    public ArtilleryFireEvent(Vector2 coordinates, string weaponType, TimeSpan detectionTime)
    {
        Id = Guid.NewGuid();
        DetectedCoordinates = coordinates;
        WeaponType = weaponType;
        DetectionTime = detectionTime;
    }

    public ArtilleryFireEvent(Vector2 coordinates, string weaponType, TimeSpan detectionTime, string artilleryType, string projectileType)
    {
        Id = Guid.NewGuid();
        DetectedCoordinates = coordinates;
        WeaponType = weaponType;
        DetectionTime = detectionTime;
        ArtilleryType = artilleryType;
        ProjectileType = projectileType;
    }

    public ArtilleryFireEvent(Vector2 coordinates, string weaponType, TimeSpan detectionTime, string artilleryType, string projectileType, int localId)
    {
        Id = Guid.NewGuid();
        DetectedCoordinates = coordinates;
        WeaponType = weaponType;
        DetectionTime = detectionTime;
        ArtilleryType = artilleryType;
        ProjectileType = projectileType;
        LocalId = localId;
    }
}
