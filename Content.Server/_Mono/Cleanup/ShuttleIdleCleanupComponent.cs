namespace Content.Server._Mono.Cleanup;

/// <summary>
///     Tracks when a shuttle was last occupied by a player. Added automatically to shuttle grids.
///     Empty shuttles are deleted by <see cref="ShuttleIdleCleanupSystem"/> after a CVar delay.
/// </summary>
// Forge-Change: idle timer for abandoned player shuttles.
[RegisterComponent]
public sealed partial class ShuttleIdleCleanupComponent : Component
{
    /// <summary>
    ///     Last time a player was aboard, approaching, or the shuttle was docked to an occupied grid.
    /// </summary>
    [ViewVariables]
    public TimeSpan LastOccupied;
}
