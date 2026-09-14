namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Raised on a gun immediately before its damage is applied.
/// </summary>
[ByRefEvent]
public record struct GunDamageModifierEvent(float Modifier);
