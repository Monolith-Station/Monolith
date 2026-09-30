using Content.Server.Electrocution;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared.Electrocution;
using Content.Shared.Power.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Server._EinsteinEngines.Power.Components;

namespace Content.Server._EinsteinEngines.Power.Systems;

public sealed partial class BatteryElectrocuteChargeSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private BatterySystem _battery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BatteryComponent, ElectrocutedEvent>(OnElectrocuted);
    }

    private void OnElectrocuted(EntityUid uid, BatteryComponent battery, ElectrocutedEvent args)
    {
        if (args.ShockDamage == null || args.ShockDamage <= 0)
            return;

        var charge = Math.Min(args.ShockDamage.Value * args.SiemensCoefficient
            / ElectrocutionSystem.ElectrifiedDamagePerWatt * 2,
                battery.MaxCharge * 0.25f)
            * _random.NextFloat(0.75f, 1.25f);

        _battery.SetCharge(uid, battery.CurrentCharge + charge);

        _popup.PopupEntity(Loc.GetString("battery-electrocute-charge"), uid, uid);
    }
}
