// Mono - whole fire
namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Raised just before a gun fires to query how fast the next shot specifically should be loaded.
/// Separate from FireRateModified to save on overhead.
/// Specifically, queries reload time, so higher = slower.
/// </summary>
[ByRefEvent]
public record struct QueryFireRateMultiplierEvent(float ReloadTimeMul = 1f);
