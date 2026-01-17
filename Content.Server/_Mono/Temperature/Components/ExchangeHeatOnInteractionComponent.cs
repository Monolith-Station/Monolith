namespace Content.Server._Mono.Temperature.Components;

/// <summary>
/// Allows for user to exchange heat with other entity while hugging them.
/// </summary>
[RegisterComponent]
public sealed partial class ExchangeHeatOnInteractionComponent : Component
{
    /// <summary>
    /// Number that will be multiplied on temperature delta
    /// </summary>
    [DataField]
    public float Coefficient = 0.25f;
}
