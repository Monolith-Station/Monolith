namespace Content.Shared._Mono.Projectile;

/// <summary>
/// Applies knockback on entities hit by this projectile.
/// </summary>
[RegisterComponent]
public sealed partial class ProjectileKnockbackComponent : Component
{
    /// <summary>
    /// Knockback, in kg*m/s.
    /// </summary>
    [DataField]
    public float Knockback = 25f;

    [DataField]
    public float RotateMultiplier = 1f; // evil
}
