using Content.Shared.Physics;

namespace Content.Shared._Mono.Weapons.Hitscan.Components;

/// <summary>
/// Similar to simple raycast, but has several entities to hit.
/// </summary>
[RegisterComponent]
public sealed partial class HitscanMultiRaycastComponent : Component
{
    /// <summary>
    /// Maximum distance the raycast will travel before giving up. Reflections will reset the distance traveled
    /// </summary>
    [DataField]
    public float MaxDistance = 20.0f;

    /// <summary>
    /// Maximum amount of entities that can be pierced
    /// </summary>
    [DataField]
    public float MaxPierce = 3f;

    /// <summary>
    /// Collision mask that will be pierced
    /// </summary>
    [DataField]
    public CollisionGroup PierceCollisionMask = CollisionGroup.MobMask;

    /// <summary>
    /// Collision mask impassable for this hitscan.
    /// </summary>
    [DataField]
    public CollisionGroup CollisionMask = CollisionGroup.Opaque;
}
