using Content.Shared.Damage;

namespace Content.Server._Mono.Weapons.Melee;

/// <summary>
/// Toggles the weapon for <see cref="ActiveTime"/> amount of time. After this time passes or melee hit is performed, <see cref="Cooldown"/> is activated
/// Used in pair with ItemToggleMeleeWeaponComponent
///  </summary>
[RegisterComponent]
public sealed partial class WeaponMeleeChargeComponent : Component
{
    [DataField]
    public float ActiveTime = 1f;

    [DataField]
    public float Cooldown = 1f;

    [DataField]
    public TimeSpan CooldownEndTime = TimeSpan.Zero;

    [DataField]
    public TimeSpan ActiveEndTime = TimeSpan.Zero;

    [DataField]
    public DamageSpecifier CooldownDamagePenalty =  new DamageSpecifier();
}
