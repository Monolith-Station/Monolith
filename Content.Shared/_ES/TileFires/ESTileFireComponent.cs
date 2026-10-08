using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._ES.TileFires;

/// <summary>
///     Handles growth behavior for tile fires, as well as things like smoldering.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class ESTileFireComponent : Component
{
    /// <summary>
    ///     Prototype to spawn when spreading.
    /// </summary>
    [DataField]
    public EntProtoId Prototype = "ESTileFire";

    [DataField]
    public float MinFirestacksToSpread = 10;

    [DataField]
    public float FirestacksRemoveOnSpread = 3;

    [DataField]
    public float BaseSpreadChance = 0.33f;

    [DataField]
    public float MinimumOxyMolesToSpread = 8f;

    /// <summary>
    ///     Minimum time after the fire spawns at which it will smolder (return to first stage and stop spreading)
    /// </summary>
    [DataField]
    public TimeSpan MinSmolderTime = TimeSpan.FromMinutes(6);

    /// <summary>
    ///     Maximum time after the fire spawns at which it will smolder, see <see cref="MinSmolderTime"/>
    /// </summary>
    [DataField]
    public TimeSpan MaxSmolderTime = TimeSpan.FromMinutes(6.25);

    /// <summary>
    ///     Chance that when a fire smolders its just deleted
    /// </summary>
    [DataField]
    public float SmolderDeleteFireChance = 0.5f;

    /// <summary>
    ///     Time chosen for this fire to smolder, using <see cref="MinSmolderTime"/> and <see cref="MaxSmolderTime"/>.
    /// </summary>
    [DataField, AutoPausedField]
    public TimeSpan SmolderTime;
}
