namespace Content.Server._Mono.GridClaimer;

/// <summary>
/// Marks this entity as claimable by grid claimers.
/// </summary>
[RegisterComponent]
public sealed partial class ClaimableGridComponent : Component
{
    /// <summary>
    /// The grid claimers keeping us claimed, if any.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> ClaimedBy = new();

    /// <summary>
    /// Whether we're currently claimed.
    /// </summary>
    [ViewVariables]
    public bool Claimed { get => ClaimedBy.Count != 0; }
}
