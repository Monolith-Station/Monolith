using Robust.Shared.GameStates;

namespace Content.Shared._Mono.ArmorPiercing;

/// <summary>
/// Component that allows projectiles to pierce through walls based on thickness. Piercing thickness is reduced by 50% after each successful pierce.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class ArmorPiercingComponent : Component
{
    /// <summary>
    /// The piercing thickness of this projectile.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int PiercingThickness = 5;

    /// <summary>
    /// Entities that this projectile has already pierced through.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> PiercedEntities = new();
}
