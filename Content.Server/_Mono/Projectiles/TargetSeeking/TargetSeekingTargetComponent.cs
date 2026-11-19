namespace Content.Server._Mono.Projectiles.TargetSeeking;

[RegisterComponent]
public sealed partial class TargetSeekingTargetComponent : Component
{
    /// <summary>
    /// If this can be targeted by any seekers without a tag whitelist.
    /// </summary>
    [DataField]
    public bool RequireSeekerWhitelist;
}
