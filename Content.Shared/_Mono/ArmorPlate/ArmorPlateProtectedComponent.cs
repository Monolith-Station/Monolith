using Robust.Shared.Timing;

namespace Content.Shared.Armor;

[RegisterComponent]

/// <summary>
/// Added or removed on plate insertion/removal or equip/unequip of any equipment with ArmorPlateHolderComponent.
/// Tying subscription of OnBeforeDamageChanged to this component for plates prevents constant spam from this system from passive regeneration and breathing from unarmored players.
/// </summary>
public sealed partial class ArmorPlateProtectedComponent : Component { }
