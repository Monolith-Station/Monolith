using Robust.Shared.GameStates;

namespace Content.Shared.Strip.Components;

/// <summary>
/// Give this to an entity when you want to decrease stripping times
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class ThievingComponent : Component
{
    /// <summary>
    /// How much the strip time should be shortened by
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("stripTimeReduction")]
    [AutoNetworkedField]
    public TimeSpan StripTimeReduction = TimeSpan.FromSeconds(0.5f);

    /// <summary>
    /// Should it notify the user if they're stripping a pocket?
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("stealthy")]
    [AutoNetworkedField]
    public bool Stealthy;

    /// <summary>
    /// Mono: Multiplies the strip time.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("timeMultiplier")]
    [AutoNetworkedField]
    public float TimeMultiplier = 1f;

    /// <summary>
    /// Mono: If true, this entity can identify hidden strip slots.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("identifyHidden")]
    [AutoNetworkedField]
    public bool IdentifyHidden;
}
