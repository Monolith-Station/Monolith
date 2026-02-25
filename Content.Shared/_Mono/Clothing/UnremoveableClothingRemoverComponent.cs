using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Clothing.Components;

/// <summary>
///     The component prohibits the player from taking off clothes on them that have this component.
/// </summary>
/// <remarks>
///     See also ClothingComponent.EquipDelay if you want the clothes that the player cannot take off by himself to be put on by the player with a delay.
///</remarks>
[NetworkedComponent, AutoGenerateComponentState]
[RegisterComponent]
[Access(typeof(UnremovableClothingSystem))]
public sealed partial class UnremovableClothingRemoverComponent : Component
{

    /// <summary>
    ///     Action used to toggle the clothing on or off.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsUnremovable = false;

}
