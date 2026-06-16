using Content.Shared.Damage;
using JetBrains.Annotations;

namespace Content.Shared._Mono.Claws.ClawTypes;

[UsedImplicitly]
public sealed partial class Declawed : ClawType
{
    [DataField]
    public float DropChanceOnMelee = 0.03f;

    [DataField]
    public TimeSpan MaxItemHoldingTime =  TimeSpan.FromSeconds(30);

    [DataField]
    public DamageSpecifier DamageOnDeclaw = new DamageSpecifier();
}
