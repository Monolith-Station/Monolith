using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
///     Raised directed on the gun entity when <see cref="SharedGunSystem.RefreshModifiers"/>
///     is called, to update the values of <see cref="GunComponent"/> from other systems.
/// </summary>
[ByRefEvent]
public record struct WieldRefreshModifiersEvent(
    Entity<GunWieldBonusComponent> Gun,
    Angle AngleIncrease,
    Angle AngleDecay,
    Angle MaxAngle,
    Angle MinAngle
);
