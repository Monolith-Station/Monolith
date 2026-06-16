namespace Content.Server._Mono.Temperature.Components;

[RegisterComponent]
public sealed partial class EntityRadiusHeaterComponent : Component
{
    [DataField]
    public float Radius = 2.5f;

    [DataField]
    public float ThermalEnergy = 5600;

    /// <summary>
    /// Thermal energy will be clamped based on this value * heat capacity.
    /// Represents temperature in kelvins.
    /// </summary>
    [DataField]
    public float Limit = 340;

    [DataField]
    public bool RequireActivation;
}
