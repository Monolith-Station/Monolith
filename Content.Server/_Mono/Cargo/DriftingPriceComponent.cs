namespace Content.Server._Mono.Cargo;

/// <summary>
/// Assigns a price to an object that drifts over time.
/// </summary>
[RegisterComponent]
public sealed partial class DriftingPriceComponent : Component
{
    /// <summary>
    /// Minimum price to initialise to.
    /// </summary>
    [DataField(required: true)]
    public double MinInitial;

    /// <summary>
    /// Maximum price to initialise to.
    /// </summary>
    [DataField(required: true)]
    public double MaxInitial;

    /// <summary>
    /// Base price to tend towards.
    /// </summary>
    [DataField(required: true)]
    public double BasePrice;

    /// <summary>
    /// Current price.
    /// </summary>
    [DataField]
    public double CurrentPrice;

    /// <summary>
    /// How much to pull the price back towards the base.
    /// 0 means do not pull at all.
    /// </summary>
    [DataField]
    public double Stability = 0.5;

    /// <summary>
    /// How fast to drift the price, fraction per second.
    /// </summary>
    [DataField]
    public double DriftRate = 0.01;
}
