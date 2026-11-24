namespace Content.Shared.Projectiles;

[ByRefEvent]
public readonly record struct ProjectileShotEvent(EntityUid? LaunchMap);
