using Content.Shared._Mono.Weapons.Ranged.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared.Weapons.Ranged.Systems;

/// <summary>
/// Mono
/// Uses inventory slot as ammo provider
/// </summary>
public abstract partial class SharedGunSystem
{
    /// <inheritdoc/>
    public void InitializeInventorySlotProvider()
    {
        SubscribeLocalEvent<InventorySlotProviderComponent, TakeAmmoEvent>(InventoryTakeAmmo);
        SubscribeLocalEvent<InventorySlotProviderComponent, GotEquippedEvent>(InventoryEquip);
        SubscribeLocalEvent<InventorySlotProviderComponent, GotUnequippedEvent>(InventoryUnEquip);
    }

    private void InventoryTakeAmmo(EntityUid uid, InventorySlotProviderComponent component, ref TakeAmmoEvent args)
    {
        if (args.User == null)
            return;

        var slotEntity = GetInventoryProviderEntity(args.User.Value, component);

        if (slotEntity == null)
            return;

        UpdateAmmoCount(uid);
        RaiseLocalEvent(slotEntity.Value, args);

        var ammoEv = new GetAmmoCountEvent();
        RaiseLocalEvent(slotEntity.Value, ref ammoEv);
    }

    private void InventoryEquip(EntityUid uid, InventorySlotProviderComponent component, ref GotEquippedEvent args)
    {
        UpdateAmmoCount(uid);
    }

    private void InventoryUnEquip(EntityUid uid, InventorySlotProviderComponent component, ref GotUnequippedEvent args)
    {
        UpdateAmmoCount(uid);
    }

    private EntityUid? GetInventoryProviderEntity(EntityUid uid, InventorySlotProviderComponent component)
    {
        if (!_inventory.TryGetSlotEntity(uid, component.Slot, out var entityUid))
            return null;

        return entityUid;
    }
}
