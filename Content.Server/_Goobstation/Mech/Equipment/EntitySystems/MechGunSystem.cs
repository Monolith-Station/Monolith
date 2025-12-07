using Content.Server.Mech.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Random;

namespace Content.Server.Mech.Equipment.EntitySystems;
public sealed class MechGunSystem : EntitySystem
{
    [Dependency] private readonly MechSystem _mech = default!;
    [Dependency] private readonly BatterySystem _battery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechEquipmentComponent, HandleMechEquipmentBatteryEvent>(OnHandleMechEquipmentBattery);
    }

    // Mono: changed
    private void OnHandleMechEquipmentBattery(EntityUid uid, MechEquipmentComponent component, HandleMechEquipmentBatteryEvent args)
    {
        if (!component.EquipmentOwner.HasValue
            || !TryComp<MechComponent>(component.EquipmentOwner.Value, out var mech)
            || !TryComp<BatteryComponent>(uid, out var battery)
        )
            return;

        var maxCharge = battery.MaxCharge;
        var currentCharge = battery.CurrentCharge;

        var chargeDelta = maxCharge - currentCharge;

        if (!_mech.TryChangeEnergy(component.EquipmentOwner.Value, -chargeDelta, mech))
            return;

        _battery.SetCharge(uid, battery.MaxCharge, battery);
    }
}
