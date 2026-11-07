using Content.Shared.Shuttles.Components;
using Content.Shared.Whitelist;

namespace Content.Shared._Mono.IFF.Components;

/// <summary>
/// Applies IFF flags to anything within (or outside, if inverted) the radius of the entity, and removes them if they exit (or enter, if inverted) that radius.
/// </summary>
[RegisterComponent]
public sealed partial class ApplyIFFFlagInRadiusComponent : Component
{
    /// <summary>
    /// The radius to apply the IFF flags within (or outside of, if inverted).
    /// </summary>
    [DataField]
    public int Radius = 5000;

    /// <summary>
    /// The flags to apply or remove.
    /// </summary>
    [DataField]
    public IFFFlags Flags = IFFFlags.HideLabelAlways;

    /// <summary>
    /// If true, the IFF flags will be applied *outside* of the radius of the entity.
    /// </summary>
    [DataField]
    public bool Inverted = false;

    [DataField]
    public EntityWhitelist? Blacklist;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    public TimeSpan LastUpdate = TimeSpan.Zero;
}