using Content.Shared.Physics;

namespace Content.Shared._Mono.Weapons.Hitscan.Components;

/// <summary>
/// This is used for...
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
    /// The collision mask the hitscan ray uses to collide with other objects. See the enum for more information
    /// </summary>
    [DataField]
    public CollisionGroup PierceCollisionMask = CollisionGroup.MobMask;

    /// <summary>
    /// The collision mask the hitscan ray uses to collide with other objects. See the enum for more information
    /// </summary>
    [DataField]
    public CollisionGroup CollisionMask = CollisionGroup.Opaque;
}
