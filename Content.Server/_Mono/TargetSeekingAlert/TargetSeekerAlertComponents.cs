using Content.Server._Mono.Projectiles.TargetSeeking;
using Robust.Shared.Audio;

namespace Content.Server._Mono.TargetSeekingAlert;

/// <summary>
///     Component for things that alert whether their grid is being tracked by something with a <see cref="TargetSeekingComponent"/>. 
/// </summary>
[RegisterComponent]
public sealed partial class TargetSeekerAlertComponent : Component
{
    /// <summary>
    ///     Sound played when a target initially starts tracking this entity. 
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? TargetGainSound = null;

    /// <summary>
    ///     List specifying information of the sound this alerter plays when the seeker closest to the entity
    ///         is close enough.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<TargetSeekerAlertSetting> DistanceAlertSettings = new();

    /// <summary>
    ///     The audio entity playing from <see cref="DistanceAlertSettings"/>. 
    /// </summary>
    public EntityUid? Audio = null;

    /// <summary>
    ///     The key of the sound specified in <see cref="DistanceAlertSounds"/> currently playing. 
    /// </summary>
    // This isn't the most amazing solution but it's necessary to prevent spamming the sound. GetHashCode is a bit overkill and it might(?) not always work either.
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float? ActiveAlertSoundKey = null;
}

/// <summary>
///     Specifies information for an entity with <see cref="TargetSeekerAlertComponent"/> about
///         what it does when a seeker is closer than <paramref name="MaximumDistance"/> from the
///         entity.
/// </summary>
[DataDefinition]
public partial record struct TargetSeekerAlertSetting()
{
    [DataField]
    public float MaximumDistance;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_Mono/Effects/target_seeker_beep.ogg");
}

/// <summary>
///     Component for grids with entities that have <see cref="TargetSeekerAlertComponent"/>'s.
/// </summary>
[RegisterComponent]
public sealed partial class TargetSeekerAlertGridComponent : Component
{
    /// <summary>
    ///     Target-seekers currently attempting to troll this grid.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public HashSet<Entity<TargetSeekingComponent, TransformComponent>> CurrentSeekers = new();

    /// <summary>
    ///     Entities <i>using</i> <see cref="TargetSeekerAlertComponent"/> on this grid.
    ///         Used to remove this component when no more such entities remain on the grid.
    /// </summary>
    // what is meant by 'using' instead of 'with': an entity can have the comp, but not actually be doing anything with it (e.g. depowered). But that may not be implemented right now so this is just future-proofed.
    // this isn't a hashset because we can actually guarantee uniqueness via logic here
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public List<EntityUid> Alerters = new();

    /// <summary>
    ///     Entities with <see cref="TargetSeekerAlertComponent"/>, that are being alerted, on this grid.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public HashSet<Entity<TargetSeekerAlertComponent>> ActiveAlerters = new();
}
