namespace Content.Server.Gatherable.Components;

/// <summary>
/// Destroys a gatherable entity when colliding with it.
/// </summary>
[RegisterComponent]
public sealed partial class GatheringProjectileComponent : Component
{
    /// <summary>
    /// How many more times we can gather.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("amount")]
    public int Amount = 1;

    /// <summary>
    /// Mono - should we break tiles on gather or not.
    /// </summary>
    [DataField]
    public bool BreakTileOnGather;
}
