using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Standing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StandingStateComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public SoundSpecifier DownSound { get; private set; } = new SoundCollectionSpecifier("BodyFall");

    // WD EDIT START
    [DataField, AutoNetworkedField]
    public StandingState CurrentState { get; set; } = StandingState.Standing;
    // WD EDIT END

    /// <summary>
    /// Mono: Chance for a projectile to miss the target if they are not standing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LyingDodgeChance = 0.5f;

    /// <summary>
    /// Mono: Range between shooter and target at where projectiles will always hit
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HitRange = 3f;

    [DataField, AutoNetworkedField]
    public bool Standing { get; set; } = true;

    /// <summary>
    ///     List of fixtures that had their collision mask changed when the entity was downed.
    ///     Required for re-adding the collision mask.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> ChangedFixtures = new();
}
// WD EDIT START
public enum StandingState
{
    Lying,
    GettingUp,
    Standing,
}
// WD EDIT END
