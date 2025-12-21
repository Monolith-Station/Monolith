using Content.Server.Power.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.PowerCell.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;
using Content.Server.Power.EntitySystems;
using Content.Server.PowerCell; // Mono

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [Dependency] private readonly BatterySystem _battery = default!; // Mono
    [Dependency] private readonly PowerCellSystem _powerCell = default!; // Mono
    protected override void InitializeBattery()
    {
        base.InitializeBattery();

        // Hitscan
        SubscribeLocalEvent<HitscanBatteryAmmoProviderComponent, ComponentStartup>(OnBatteryStartup);
        SubscribeLocalEvent<HitscanBatteryAmmoProviderComponent, ChargeChangedEvent>(OnBatteryChargeChange);
        SubscribeLocalEvent<HitscanBatteryAmmoProviderComponent, DamageExamineEvent>(OnBatteryDamageExamine);
        SubscribeLocalEvent<HitscanBatteryAmmoProviderComponent, PowerCellChangedEvent>(OnPowerCellChanged);

        // Projectile
        SubscribeLocalEvent<ProjectileBatteryAmmoProviderComponent, ComponentStartup>(OnBatteryStartup);
        SubscribeLocalEvent<ProjectileBatteryAmmoProviderComponent, ChargeChangedEvent>(OnBatteryChargeChange);
        SubscribeLocalEvent<ProjectileBatteryAmmoProviderComponent, DamageExamineEvent>(OnBatteryDamageExamine);
        SubscribeLocalEvent<ProjectileBatteryAmmoProviderComponent, PowerCellChangedEvent>(OnPowerCellChanged);
    }

    private void OnBatteryStartup(EntityUid uid, BatteryAmmoProviderComponent component, ComponentStartup args)
    {
        UpdateShots(uid, component);
    }

    private void OnBatteryChargeChange(EntityUid uid, BatteryAmmoProviderComponent component, ref ChargeChangedEvent args)
    {
        UpdateShots(uid, component, args.Charge, args.MaxCharge);
    }
    // Mono Start - Call UpdateShots when a power cell is added/removed/changed
    private void OnPowerCellChanged(EntityUid uid, BatteryAmmoProviderComponent component, PowerCellChangedEvent args)
    {
        UpdateShots(uid, component);
    }
    // Mono End

    // Mono Start - Call UpdateShots on internal battery if available, if not call using a power cell
    private void UpdateShots(EntityUid uid, BatteryAmmoProviderComponent component)
    {
        if (TryComp<BatteryComponent>(uid, out var battery))
        {
            UpdateShots(uid, component, battery.CurrentCharge, battery.MaxCharge);
            return;
        }

        if (_powerCell.TryGetBatteryFromSlot(uid, out var cellBattery))
        {
            UpdateShots(uid, component, cellBattery.CurrentCharge, cellBattery.MaxCharge);
        }
        else
        {
            UpdateShots(uid, component, 0, component.Capacity * component.FireCost);
        }
    }
    // Mono End

    private void UpdateShots(EntityUid uid, BatteryAmmoProviderComponent component, float charge, float maxCharge)
    {
        var shots = (int)(charge / component.FireCost);
        var maxShots = (int)(maxCharge / component.FireCost);

        if (component.Shots != shots || component.Capacity != maxShots)
        {
            Dirty(uid, component);
        }

        component.Shots = shots;
        component.Capacity = maxShots;
        UpdateBatteryAppearance(uid, component);
    }

    private void OnBatteryDamageExamine(EntityUid uid, BatteryAmmoProviderComponent component, ref DamageExamineEvent args)
    {
        var damageSpec = GetDamage(component);

        if (damageSpec == null)
            return;

        var damageType = component switch
        {
            HitscanBatteryAmmoProviderComponent => Loc.GetString("damage-hitscan"),
            ProjectileBatteryAmmoProviderComponent => Loc.GetString("damage-projectile"),
            _ => throw new ArgumentOutOfRangeException(),
        };

        _damageExamine.AddDamageExamine(args.Message, Damageable.ApplyUniversalAllModifiers(damageSpec), damageType);
    }

    private DamageSpecifier? GetDamage(BatteryAmmoProviderComponent component)
    {
        if (component is ProjectileBatteryAmmoProviderComponent battery)
        {
            if (ProtoManager.Index<EntityPrototype>(battery.Prototype).Components
                .TryGetValue(Factory.GetComponentName<ProjectileComponent>(), out var projectile))
            {
                var p = (ProjectileComponent)projectile.Component;

                if (!p.Damage.Empty)
                {
                    return p.Damage * Damageable.UniversalProjectileDamageModifier;
                }
            }

            return null;
        }

        if (component is HitscanBatteryAmmoProviderComponent hitscan)
        {
            var dmg = ProtoManager.Index(hitscan.HitscanEntityProto);
            if (!dmg.TryGetComponent<HitscanBasicDamageComponent>(out var basicDamageComp, Factory))
                return null;

            return basicDamageComp.Damage * Damageable.UniversalHitscanDamageModifier;
        }

        return null;
    }

    // Mono Start - Reduce charge in internal battery, reduce in power cell if not available
    protected override void TakeCharge(EntityUid uid, BatteryAmmoProviderComponent component)
    {
        if (TryComp<BatteryComponent>(uid, out var battery))
        {
            _battery.UseCharge(uid, component.FireCost);
            return;
        }

        if (TryComp<PowerCellSlotComponent>(uid, out var powerCellSlot))
        {
            _powerCell.TryUseCharge(uid, component.FireCost, powerCellSlot);
        }
    }
    // Mono End
}
