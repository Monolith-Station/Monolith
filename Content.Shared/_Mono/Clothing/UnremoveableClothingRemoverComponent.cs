using Content.Shared.Clothing.EntitySystems;
using Robust.Shared.GameStates;
using Content.Shared.Whitelist;

namespace Content.Shared.Clothing.Components;

/// <summary>
///     The component prohibits the player from taking off clothes on them that have this component.
/// </summary>
[NetworkedComponent, AutoGenerateComponentState]
[RegisterComponent]
[Access(typeof(UnremovableClothingSystem))]
public sealed partial class UnremovableClothingRemoverComponent : Component
{

    /// <summary>
    /// Toggles the unremoveability of clothing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsUnremovable = false;

    [DataField, AutoNetworkedField]

    public EntityWhitelist? Whitelist = null;

}
