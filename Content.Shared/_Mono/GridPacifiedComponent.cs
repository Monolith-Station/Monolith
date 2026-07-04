using Robust.Shared.GameStates;

namespace Content.Shared._Mono;

/// <summary>
/// Component that applies Pacified status to all organic entities on a grid.
/// Entities with company affiliations matching the exempt companies will not be pacified.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class GridPacifiedComponent : Component
{
    /// <summary>
    /// A check for if an entity is pre-pacified, such as by having the pacified trait.
    /// </summary>
    [DataField]
    public bool PrePacified = false;

    /// <summary>
    /// Until what time an entity will be pacified for. The component is removed when this is exceeded.
    /// </summary>
    [DataField, AutoPausedField]
    public TimeSpan PacifiedTime;

    /// <summary>
    /// The time when the next periodic update should occur
    /// </summary>
    [DataField, AutoPausedField]
    public TimeSpan NextUpdate;

    /// <summary>
    /// How frequently to check the entity for changes
    /// </summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The radius from a GridPacifier entity that a GridPacified entity is pacified.
    /// </summary>
    [DataField]
    public float PacifyRadius = 256f;
}
