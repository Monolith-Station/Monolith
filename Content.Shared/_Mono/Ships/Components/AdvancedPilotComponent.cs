namespace Content.Shared._Mono.Ships.Components;

/// <summary>
/// Marker component.
/// Entities with this component can use both gunnery and shuttle consoles at once even on ships that would normally force you to use one at a time.
/// </summary>
[RegisterComponent]
public sealed partial class AdvancedPilotComponent : Component { }
