using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Detection;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DetectionRangeMultiplierComponent : Component
{
    /// <summary>
    ///     Multiplier of infrared detection radius.
    ///     Infrared detection radius scales with the square root of the grid's heat.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float InfraredMultiplier = 1f;

    /// <summary>
    ///     In what portion of infrared detection radius to start showing the grid outline.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float InfraredOutlinePortion = 0.6f;

    /// <summary>
    ///     Multiplier of visual detection radius.
    ///     Visual detection radius scales linearly off a grid's diagonal.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float VisualMultiplier = 1f;

    /// <summary>
    ///     Whether to have effectively infinite detection range.
    ///     Use for aghosts.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AlwaysDetect;
}
