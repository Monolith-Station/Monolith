using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.ShipRepair.Components;

/// <summary>
/// Allows item to act as a tool that repairs missing tiles and entities of a grid with <see cref="ShipRepairDataComponent">.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipRepairToolComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool EnableTileRepair = true;

    [DataField, AutoNetworkedField]
    public bool EnableEntityRepair = true;

    /// <summary>
    /// Multiplier of time the repair doafter should take.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RepairTimeMultiplier = 1f;

    /// <summary>
    /// Length of doafter when repairing tiles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TileRepairTime = 0.5f;

    /// <summary>
    /// Charges to take from <see cref="LimitedChargesComponent"> when repairing tiles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int TileRepairCost = 1;

    /// <summary>
    /// In what radius to search for entities to repair on click.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float EntitySearchRadius = 0.5f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? RepairSound = new SoundPathSpecifier("/Audio/Items/deconstruct.ogg");

    /// <summary>
    /// List of active doafters, used to allow clicking on the same tile multiple times to repair different missing entities on it.
    /// </summary>
    [ViewVariables]
    public List<DoAfterId> DoAfters = new();

    /// <summary>
    /// Effect to spawn on start repair.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId ConstructEffect = "EffectRCDConstruct1";

    [DataField, AutoNetworkedField]
    public float GhostRenderRadius = 8f;
}
