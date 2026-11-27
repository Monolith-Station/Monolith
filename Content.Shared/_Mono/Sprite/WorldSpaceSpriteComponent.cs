namespace Content.Shared._Mono.Sprite;

/// <summary>
/// Picks directional sprite frames from the entity's grid-local rotation instead of the eye, keeping them in world space.
/// </summary>
[RegisterComponent]
public sealed partial class WorldSpaceSpriteComponent : Component;
