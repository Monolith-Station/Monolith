using Robust.Shared.GameStates;

namespace Content.Shared.Light.Components;

/// <summary>
/// Will draw shadows over tiles flagged as roof tiles on the attached grid. ImplicitRoofComponent will get removed if the grid has this component.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RoofComponent : Component
{
    public const int ChunkSize = 8;

    [DataField, AutoNetworkedField]
    public Color Color = Color.Black;

    /// <summary>
    /// Chunk origin and bitmask of value in chunk.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<Vector2i, ulong> Data = new();

    // Rebuilt from the Z-network; never baked into a saved map's authored roofs.
    [AutoNetworkedField]
    public Dictionary<Vector2i, ulong> ZLevelData = new();
}
