namespace Content.Server._Mono.Detection;

/// <summary>
///     Component that gives an entity a thermal signature while it's powered.
/// </summary>
[RegisterComponent]
public sealed partial class MachineThermalSignatureComponent : Component
{
    [DataField(required: true)]
    public float Signature;
}
