using System.Linq;
using Content.Shared.Roles;
using Content.Shared.Radio.Components;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Humanoid;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;

namespace Content.Server._Mono;

/// <summary>
/// System that automatically slots encryption keys from loadouts into headsets and IPCs during spawn.
/// </summary>
public sealed partial class LoadoutEncryptionKeySlottingSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Listen for when starting gear is equipped on any character
        SubscribeLocalEvent<HumanoidAppearanceComponent, StartingGearEquippedEvent>(OnStartingGearEquipped);
    }

    private void OnStartingGearEquipped(EntityUid uid, HumanoidAppearanceComponent component, ref StartingGearEquippedEvent args)
    {
        // Find all encryption keys in the character's storage and slot them appropriately
        var encryptionKeys = FindEncryptionKeysInStorage(uid);

        if (encryptionKeys.Count == 0)
            return;

        // Check if this character is an IPC
        if (TryComp<EncryptionKeyHolderComponent>(uid, out var ipcKeyHolder))
        {
            // This is an IPC, slot keys into their internal encryption key holder
            SlotKeysIntoIPC(uid, ipcKeyHolder, encryptionKeys);
        }
        else
        {
            // This is an organic, slot keys into their headset
            SlotKeysIntoHeadset(uid, encryptionKeys);
        }
    }

    /// <summary>
    /// Finds all encryption keys in the character's storage containers (backpack, belt, etc.)
    /// </summary>
    private List<EntityUid> FindEncryptionKeysInStorage(EntityUid character)
    {
        var encryptionKeys = new List<EntityUid>();

        // Check all inventory slots for storage containers
        if (!_inventory.TryGetSlots(character, out var slots))
            return encryptionKeys;

        foreach (var slot in slots)
        {
            if (!_inventory.TryGetSlotEntity(character, slot.Name, out var slotEntity))
                continue;

            // Check if this slot contains a storage container
            if (!TryComp<StorageComponent>(slotEntity, out var storage))
                continue;

            // Search through all items in this storage container
            var container = storage.Container;

            foreach (var item in container.ContainedEntities.ToList())
            {
                if (HasComp<EncryptionKeyComponent>(item))
                {
                    encryptionKeys.Add(item);
                }
            }
        }

        return encryptionKeys;
    }

    /// <summary>
    /// Slots encryption keys into an IPC's internal encryption key holder
    /// </summary>
    private void SlotKeysIntoIPC(EntityUid ipc, EncryptionKeyHolderComponent keyHolder, List<EntityUid> encryptionKeys)
    {
        var slotsAvailable = keyHolder.KeySlots - keyHolder.KeyContainer.ContainedEntities.Count;
        var keysToSlot = Math.Min(slotsAvailable, encryptionKeys.Count);

        for (int i = 0; i < keysToSlot; i++)
        {
            var key = encryptionKeys[i];

            // Remove from storage first
            if (!RemoveFromStorage(key))
            {
                Log.Warning($"Failed to remove encryption key {ToPrettyString(key)} from storage for IPC {ToPrettyString(ipc)}");
                continue;
            }

            // Insert into IPC's internal key holder
            if (!_container.Insert(key, keyHolder.KeyContainer))
            {
                Log.Warning($"Failed to insert encryption key {ToPrettyString(key)} into IPC {ToPrettyString(ipc)}");
                // Try to put it back in storage if insertion failed
                // This is a best-effort cleanup
                continue;
            }

            Log.Debug($"Successfully slotted encryption key {ToPrettyString(key)} into IPC {ToPrettyString(ipc)}");
        }
    }

    /// <summary>
    /// Slots encryption keys into a character's headset
    /// </summary>
    private void SlotKeysIntoHeadset(EntityUid character, List<EntityUid> encryptionKeys)
    {
        // Find the headset in the ears slot
        if (!_inventory.TryGetSlotEntity(character, "ears", out var headsetEntity))
            return;

        if (!TryComp<EncryptionKeyHolderComponent>(headsetEntity, out var keyHolder))
            return;

        var slotsAvailable = keyHolder.KeySlots - keyHolder.KeyContainer.ContainedEntities.Count;
        var keysToSlot = Math.Min(slotsAvailable, encryptionKeys.Count);

        for (int i = 0; i < keysToSlot; i++)
        {
            var key = encryptionKeys[i];

            // Remove from storage first
            if (!RemoveFromStorage(key))
            {
                Log.Warning($"Failed to remove encryption key {ToPrettyString(key)} from storage for character {ToPrettyString(character)}");
                continue;
            }

            // Insert into headset's key holder
            if (!_container.Insert(key, keyHolder.KeyContainer))
            {
                Log.Warning($"Failed to insert encryption key {ToPrettyString(key)} into headset {ToPrettyString(headsetEntity.Value)} for character {ToPrettyString(character)}");
                // Try to put it back in storage if insertion failed
                // This is a best-effort cleanup
                continue;
            }

            Log.Debug($"Successfully slotted encryption key {ToPrettyString(key)} into headset {ToPrettyString(headsetEntity.Value)} for character {ToPrettyString(character)}");
        }
    }

    /// <summary>
    /// Removes an encryption key from whatever storage container it's currently in
    /// </summary>
    private bool RemoveFromStorage(EntityUid encryptionKey)
    {
        // Find the container this key is in
        if (!_container.TryGetContainingContainer(encryptionKey, out var container))
            return false;

        // Remove from the container
        return _container.Remove(encryptionKey, container);
    }
}
