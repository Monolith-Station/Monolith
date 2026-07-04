namespace Content.Server._NF.Trade;

/// <summary>
/// This marks a station as matching all trade crate destinations.
/// Useful, for example, for black market stations or pirate coves.
/// </summary>
[RegisterComponent]
public sealed partial class TradeCrateWildcardDestinationComponent : Component
{
    /// <summary>
    /// This multiplies the value of crates sold at wildcard destinations. - Mono
    /// </summary>
    [DataField("valueMultiplier")]
    public float ValueMultiplier = 1f;

};
