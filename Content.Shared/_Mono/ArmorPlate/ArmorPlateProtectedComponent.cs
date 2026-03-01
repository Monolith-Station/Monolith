using Robust.Shared.Timing;

namespace Content.Shared.Armor;

[RegisterComponent]

/// <summary>
/// Added or removed on plate insertion/removal or equip/unequip of any equipment with ArmorPlateHolderComponent.
/// Tying subscription of OnBeforeDamageChanged to this component for plates prevents constant spam from this system from passive regeneration and breathing from unarmored players.
/// </summary>
public sealed partial class ArmorPlateProtectedComponent : Component
{    /// <summary>
     /// Disambiguate between damage from metabolism VS an explosion by smelling when an explosion occured against a plate user.
     /// Technically possible to frame-perfect parry a metabolism tick with an explosion but why
     /// </summary>
    public GameTick LastExplosionTick;
}
