using Content.Shared.EntityEffects;

namespace Content.Server._Mono.Temperature.Components;
/// <summary>
/// Component that holds dictionary of temperatures and status effects.
/// </summary>
[RegisterComponent]
public sealed partial class TemperatureStatusEffectsComponent : Component
{
    [DataField]
    public List<TemperatureEffect> TemperatureEffects = [];
}

[DataDefinition, Serializable]
public sealed partial class TemperatureEffect
{
    [DataField]
    public float MinimumTemperature = float.NegativeInfinity;

    [DataField]
    public float MaximumTemperature = float.MaxValue;

    [DataField(required: true)]
    public List<EntityEffect> Effects = default!;
}
