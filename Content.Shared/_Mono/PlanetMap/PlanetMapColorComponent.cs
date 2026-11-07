using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.PlanetMap;

/// <summary>
/// Overrides how an entity placed by a biome entity layer is coloured on planet radar views and the shuttle console map.
/// </summary>
[RegisterComponent]
public sealed partial class PlanetMapColorComponent : Component
{
    [DataField]
    public Color? Color;

    [DataField]
    public EntProtoId? ColorFrom;

    /// <summary>
    /// Never draw this entity.
    /// </summary>
    [DataField]
    public bool Hidden;
}
