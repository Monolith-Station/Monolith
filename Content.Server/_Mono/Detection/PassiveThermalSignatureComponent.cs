namespace Content.Server._Mono.Detection;

/// <summary>
///     Component that gives an entity a static and constant thermal signature.
/// </summary>
[RegisterComponent]
public sealed partial class PassiveThermalSignatureComponent : Component
{
    [DataField(required: true)]
    public float Signature;
}
