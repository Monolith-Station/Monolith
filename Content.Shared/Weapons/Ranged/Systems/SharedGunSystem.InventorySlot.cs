using Content.Shared._Mono.Weapons.Ranged.Components;
using Content.Shared.Inventory;
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
    }

    private void InventoryTakeAmmo(EntityUid uid, InventorySlotProviderComponent component, ref TakeAmmoEvent args)
    {

    }

    private EntityUid? GetInventoryProviderEntity(EntityUid uid, InventorySlotProviderComponent component)
    {
        if (!_inventory.TryGetSlotEntity(uid, component.Slot, out var entityUid))
            return null;

        return entityUid;
    }
}
