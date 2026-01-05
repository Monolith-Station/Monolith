using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared._Mono.ArmorPlate;
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
        if (args.Cancelled || args.Damage.Empty)
            return;

        if (!args.Damage.DamageDict.TryGetValue("Piercing", out var piercingDamage) || piercingDamage <= 0)
            return;

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

            AbsorbDamage(ent, equipped.Value, holder, plate, piercingDamage);

            args.Damage.DamageDict.Remove("Piercing");

            return;
        }
    }

    private void AbsorbDamage(
        EntityUid wearer,
        EntityUid armorUid,
        ArmorPlateHolderComponent holder,
        Entity<ArmorPlateItemComponent> plate,
        Shared.FixedPoint.FixedPoint2 damage)
    {
        var damageSpec = new DamageSpecifier();
        damageSpec.DamageDict.Add("Blunt", damage);

        _damageable.TryChangeDamage(plate.Owner, damageSpec, ignoreResistances: true);

        var staminaDamage = damage.Float() * plate.Comp.StaminaDamageMultiplier;
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
