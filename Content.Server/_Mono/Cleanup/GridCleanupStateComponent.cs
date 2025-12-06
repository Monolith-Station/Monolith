namespace Content.Server._Mono.Cleanup;

/// <summary>
///     Stores at which time will we have to be still meeting cleanup conditions for this grid to get cleaned up.
/// </summary>
[RegisterComponent]
public sealed partial class GridCleanupStateComponent : Component
{
    [ViewVariables]
    public TimeSpan CleanupAccumulator = TimeSpan.FromSeconds(0);
}
