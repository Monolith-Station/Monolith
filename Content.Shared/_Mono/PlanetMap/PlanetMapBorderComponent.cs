using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Mono.PlanetMap;

/// <summary>
/// Circular world border on a map.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlanetMapBorderComponent : Component
{
    /// <summary>
    /// Border radius in tiles.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public float Radius = 1024f;

    /// <summary>
    /// Centre of the border to the map origin.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 Offset = Vector2.Zero;
}
