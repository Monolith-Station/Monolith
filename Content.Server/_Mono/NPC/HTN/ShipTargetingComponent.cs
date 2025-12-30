using Robust.Shared.Map;
using System.Numerics;

namespace Content.Server._Mono.NPC.HTN;

/// <summary>
/// Added to entities that are controlling their ship parent to fire guns.
/// </summary>
[RegisterComponent]
public sealed partial class ShipTargetingComponent : Component
{
    /// <summary>
    /// Coordinates we're targeting.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public EntityCoordinates Target;

    /// <summary>
    /// How good to lead the target.
    /// </summary>
    [DataField]
    public float LeadingAccuracy = 1f;

    /// <summary>
    /// Velocity we're currently estimating for imperfect target leading.
    /// </summary>
    [DataField]
    public Vector2 CurrentLeadingVelocity = Vector2.Zero;

    /// <summary>
    /// Cached list of cannons we'll try to fire.
    /// </summary>
    [DataField]
    public List<EntityUid> Cannons = new();
}
