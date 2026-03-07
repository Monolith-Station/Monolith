using Content.Shared.Damage;
using JetBrains.Annotations;

namespace Content.Shared._Mono.Claws.ClawTypes;

[UsedImplicitly]
public sealed partial class SharpClaw : ClawType
{
    [DataField]
    public DamageSpecifier? Damage = new DamageSpecifier();

    [DataField]
    public float GunSpreadMultiplier = 1f;

    [DataField]
    public DamageModifierSet MeleeDamageModifiers = new DamageModifierSet();

    [DataField]
    public bool CanWideSwing;

    [DataField]
    public bool CanShoot = true;
}
