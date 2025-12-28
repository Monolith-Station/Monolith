using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Trigger;

/// <summary>
///     Creates random lightnings on trigger.
/// </summary>
[RegisterComponent]
public sealed partial class LightningOnTriggerComponent : Component
{
    /// <summary>
    ///     Chance of creating lightnings at all.
    /// </summary>
    [DataField]
    public float Chance = 1f;

    /// <summary>
    ///     How many lightnings to create.
    /// </summary>
    [DataField]
    public int Count = 1;

    /// <summary>
    ///     Maximum range of the lightnings.
    /// </summary>
    [DataField]
    public float Range = 10f;

    /// <summary>
    ///     How many recursive arcs should the lightnings have.
    /// </summary>
    [DataField]
    public int ArcDepth = 0;

    /// <summary>
    ///     Whether to trigger lightning effects (e.g. explosions) on hit.
    /// </summary>
    [DataField]
    public bool LightningEffects = true;

    /// <summary>
    ///     Lightning prototype to use.
    /// </summary>
    [DataField]
    public EntProtoId LightningProto = "Lightning";
}
