using Content.Shared._Mono.ArmorPlate;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Containers;
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

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventoryComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<ArmorPlateItemComponent, EntityTerminatingEvent>(OnPlateDestroyed);
    }

    private void OnBeforeDamageChanged(Entity<InventoryComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled || !args.Damage.AnyPositive())
            return;

        if (args.Origin == null)
            return;

        var rawDamage = new List<(string type, FixedPoint2 amount)>();
        foreach (var (type, amount) in args.Damage.DamageDict)
        {
            if (amount > FixedPoint2.Zero)
                rawDamage.Add((type, amount));
        }

        if (!_inventory.TryGetSlots(ent, out var slots))
            return;

        foreach (var slot in slots)
        {
            if (!_inventory.TryGetSlotEntity(ent, slot.Name, out var equipped, ent.Comp))
                continue;

            if (!TryComp<ArmorPlateHolderComponent>(equipped, out var holder))
                continue;

            if (!TryGetActivePlate((equipped.Value, holder), out var plate))
                continue;

            var remainderSpec = new DamageSpecifier();

            foreach (var (type, amount) in rawDamage)
            {
                // Damage values handled for plate and wearer
                var multiplier = plate.Comp.DamageMultipliers.GetValueOrDefault(type, 1.0f);
                var ratio = plate.Comp.AbsorptionRatios.GetValueOrDefault(type, 0f);

                FixedPoint2 absorbed = FixedPoint2.Zero;
                FixedPoint2 remainder = amount;

                // Handler for protection penalties: negative absorption ratios have a positive
                // Absorption value to plates for the purpose of damaging it.
                if (ratio > 0f)
                {
                    absorbed = amount * ratio;
                    remainder = amount - absorbed;
                }
                else if (ratio < 0f)
                {
                    remainder = amount * (1f + Math.Abs(ratio));
                }

                // Apply damage to plate
                var plateDamage = amount * Math.Abs(ratio) * multiplier;
                if (absorbed > FixedPoint2.Zero)
                    AbsorbDamage(ent, equipped.Value, holder, plate, absorbed, plateDamage);

                // Prepare wearer remainder
                if (remainder > FixedPoint2.Zero)
                    remainderSpec.DamageDict.Add(type, remainder);
            }


            // Replace raw damage with remaining damage post-absorption
            args.Damage.DamageDict.Clear();
            foreach (var (type, amt) in remainderSpec.DamageDict)
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
}
