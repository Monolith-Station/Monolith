using System.Numerics;

namespace Content.Shared._Mono.Fixtures;

/// <summary>
/// Swaps a polygon fixture (and optionally the occluder) based on the entity's local cardinal direction.
/// This is so diagonal walls on Mono have a proper collider.
/// </summary>
[RegisterComponent]
public sealed partial class DirectionalHullComponent : Component
{
    [DataField]
    public string Fixture = "fix1";

    [DataField(required: true)]
    public Dictionary<Direction, Vector2[]> Hulls = new();

    [DataField]
    public bool Occluder = true;
}
