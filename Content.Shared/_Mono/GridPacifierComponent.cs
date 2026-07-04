using Robust.Shared.GameStates;

namespace Content.Shared._Mono;

/// <summary>
/// Component that applies Pacified status to all organic entities on a grid.
/// Entities with company affiliations matching the exempt companies will not be pacified.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GridPacifierComponent : Component
{
}
