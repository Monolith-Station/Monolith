using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Weapons.Ranged.Components;

/// <summary>
/// Marker component for projectiles that should ignore collisions with originating grid or other projectiles with the same originating grid.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ProjectileGridPhaseComponent : Component
{
    /// <summary>
    /// The grid the projectile was spawned from.
    /// </summary>
    [ViewVariables]
    public EntityUid? SourceGrid;
}
