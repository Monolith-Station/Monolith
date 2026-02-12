using Content.Shared._Mono.ArmorPlate;
using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Mono.ArmorPlate;

/// <summary>
/// Handles armor plate absorption and deletion.
/// </summary>
public sealed class ArmorPlateSystem : SharedArmorPlateSystem
{
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlateProtectedComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<ArmorPlateHolderComponent, GetExplosionResistanceEvent>(OnExplosionResistance);
        SubscribeLocalEvent<ArmorPlateItemComponent, EntityTerminatingEvent>(OnPlateDestroyed);
    }

    private void OnBeforeDamageChanged(Entity<PlateProtectedComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled || !args.Damage.AnyPositive())
            return;

        if (!TryComp<InventoryComponent>(ent.Owner, out var inv))
            return;

        if (!_inventory.TryGetSlots(ent, out var slots))
            return;

        if (args.Origin == null)
        {
            var explosionExists = false;
            foreach (var slot in slots)
            {
                if (!_inventory.TryGetSlotEntity(ent, slot.Name, out var equipped, inv))
                    continue;

                if (!TryComp<ArmorPlateHolderComponent>(equipped, out var holder))
                    continue;

                if (holder.LastExplosionTick == _timing.CurTick)
                {
                    explosionExists = true;
                    break;
                }
            }

            if (!explosionExists)
                return;
        }

        foreach (var slot in slots)
        {
            if (!_inventory.TryGetSlotEntity(ent, slot.Name, out var equipped, inv))
                continue;

            if (!TryComp<ArmorPlateHolderComponent>(equipped, out var holder))
                continue;

            if (!TryGetActivePlate((equipped.Value, holder), out var plate))
                continue;

            CalcPlateDamages( args.Damage, plate.Comp, out var remainder, out var absorbed, out var plateDamage);

            AbsorbDamage(ent, equipped.Value, holder, plate, absorbed, plateDamage);

            // Replace raw damage with remaining damage post-absorption
            args.Damage.DamageDict.Clear();
            foreach (var (type, amt) in remainder.DamageDict)
                args.Damage.DamageDict.Add(type, amt);

            if (args.Damage.Empty)
                args.Cancelled = true;
        }
    }

    private void AbsorbDamage(
        EntityUid wearer,
        EntityUid armorUid,
        ArmorPlateHolderComponent holder,
        Entity<ArmorPlateItemComponent> plate,
        FixedPoint2 absorbed,
        FixedPoint2 plateDamage)
    {
        var damageSpec = new DamageSpecifier();
        damageSpec.DamageDict.Add("Blunt", plateDamage);

        _damageable.TryChangeDamage(plate.Owner, damageSpec, ignoreResistances: true);

        var staminaDamage = absorbed.Float() * plate.Comp.StaminaDamageMultiplier;
        _stamina.TakeStaminaDamage(wearer, staminaDamage);
    }

    private void OnPlateDestroyed(Entity<ArmorPlateItemComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!_container.TryGetContainingContainer(ent.Owner, out var container))
            return;

        var holderUid = container.Owner;
        if (!TryComp<ArmorPlateHolderComponent>(holderUid, out var holder))
            return;

        if (holder.ActivePlate != ent.Owner)
            return;

        if (holder.ShowBreakPopup)
        {
            if (_inventory.TryGetContainingEntity(holderUid, out var wearer))
            {
                var plateName = MetaData(ent).EntityName;
                _popup.PopupEntity(
                    Loc.GetString("armor-plate-break", ("plateName", plateName)),
                    wearer.Value,
                    wearer.Value,
                    PopupType.MediumCaution
                );
            }
        }
    }

    //used to ascertain if damage with no origin entity uid is an explosion or a non-direct source (rads,fire,metabolism)
    private void OnExplosionResistance(EntityUid uid, ArmorPlateHolderComponent comp, ref GetExplosionResistanceEvent args)
    {
        comp.LastExplosionTick = _timing.CurTick;
    }
}
