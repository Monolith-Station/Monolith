namespace Content.Server._Mono.Temperature.Components;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class EntityRadiusHeaterComponent : Component
{
    [DataField]
    public float Radius = 3f;

    [DataField]
    public float ThermalEnergy = 5600;

    [DataField]
    public bool RequireActivation = false;
}
