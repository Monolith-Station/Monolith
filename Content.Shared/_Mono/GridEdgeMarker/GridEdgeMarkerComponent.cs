using System.Numerics;

namespace Content.Shared._Mono.GridEdgeMarker;

/// <summary>
///     Causes this entity to display as a grid edge on radar interfaces, even inside grids.
/// </summary>
[RegisterComponent]
public sealed partial class GridEdgeMarkerComponent : Component
{
    [DataField(required: true)]
    public Vector2 Begin;

    [DataField(required: true)]
    public Vector2 End;
}
