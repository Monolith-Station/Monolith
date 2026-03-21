using Content.Shared.Clothing.EntitySystems;
using Robust.Shared.GameStates;
using Content.Shared.Whitelist;

namespace Content.Shared.Clothing.Components;

/// <summary>
/// The component toggles the UnremoveableClothingComponent.
/// </summary>
[NetworkedComponent]
[RegisterComponent]
[Access(typeof(UnremovableClothingSystem))]
public sealed partial class UnremovableClothingRemoverComponent : Component
{
    /// <summary>
    /// Whitelist for UnremoveableClothingComponent entities. If null, works on all entities with the component.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist = null;
}
