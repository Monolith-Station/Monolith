namespace Content.Server._Mono.Temperature.Components;

/// <summary>
/// Allows for user to exchange heat with other entity while hugging them.
/// </summary>
[RegisterComponent]
public sealed partial class ExchangeHeatOnInteractionComponent : Component
{
    /// <summary>
    /// Number that will be multiplied on temperature delta
    /// = 1 means that temperatures will swap.
    /// less than 1 means that temperatures will exchange their energy.
    /// less than 0 or more than 1 means that you are a retard
    /// </summary>
    [DataField]
    public float Coefficient = 0.25f;
}
