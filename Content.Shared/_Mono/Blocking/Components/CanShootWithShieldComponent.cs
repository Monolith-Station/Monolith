namespace Content.Shared._Mono.Blocking.Components;

/// <summary>
/// Marks that this gun can be used alongside a shield. Note that this allows somebody to DOUBLE their effective health while using this gun, so do not add it willy-nilly.
/// </summary>
[RegisterComponent]
public sealed partial class CanShootWithShieldComponent : Component;
