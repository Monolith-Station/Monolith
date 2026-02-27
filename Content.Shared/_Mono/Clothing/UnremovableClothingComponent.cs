using Content.Shared.Clothing.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared.Clothing.Components;

/// <summary>
/// The component prohibits the player from taking off clothes on them that have this component unless toggled by UnremoveableClothingRemoverComponent with a whitelist.
/// </summary>
[NetworkedComponent, AutoGenerateComponentState]
[RegisterComponent]
[Access(typeof(UnremovableClothingSystem))]
public sealed partial class UnremovableClothingComponent : Component
{
    /// <summary>
    /// Toggles the unremoveability of clothing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsUnremovable = true;
}
