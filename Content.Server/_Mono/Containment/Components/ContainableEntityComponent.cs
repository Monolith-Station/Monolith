namespace Content.Server._Mono.Containment.Components;

[RegisterComponent]
public sealed partial class ContainableEntityComponent : Component
{
    [DataField]
    public float BasePoints = 10f;

    [DataField]
    public float Multiplier = 1f;

    [DataField]
    public float MultiplierDecay = 0.0003f;
}
