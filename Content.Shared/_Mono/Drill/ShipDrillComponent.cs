using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Drill;

/// <summary>
/// Drill that destroys tiles and damages structures on its way. Additional behaviour is declared by DrillType
/// </summary>
[RegisterComponent]
public sealed partial class ShipDrillComponent : Component
{
    [DataField]
    public int DrillOffsetX = 0;

    [DataField]
    public int DrillOffsetY = 5;

    [DataField]
    public int DrillLength = 15;

    [DataField]
    public int DrillWidth = 6;

    [DataField]
    public DamageSpecifier? Damage = new DamageSpecifier();

    [DataField]
    public DamageSpecifier? SelfDamage = new DamageSpecifier();
}
