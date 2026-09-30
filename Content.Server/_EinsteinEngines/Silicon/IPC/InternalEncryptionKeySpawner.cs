using Content.Shared.Roles;
using Content.Shared.Radio.Components;
using Content.Shared.Containers;
using Robust.Shared.Containers;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Inventory;
using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Server._EinsteinEngines.Silicon.IPC;
public sealed partial class InternalEncryptionKeySpawner : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EncryptionKeyHolderComponent, StartingGearEquippedEvent>(OnStartingGearEquipped);
    }

    private void OnStartingGearEquipped(EntityUid uid, EncryptionKeyHolderComponent component, ref StartingGearEquippedEvent args)
    {
        // This is for loadout headsets, which are equipped directly to the IPC
        // Check if there's a headset in the "ears" slot
        if (_inventory.TryGetSlotEntity(uid, "ears", out var headsetEntity) &&
            TryComp<EncryptionKeyHolderComponent>(headsetEntity, out var headsetKeyHolder))
        {
            // Copy encryption keys from the headset to the IPC
            CopyEncryptionKeys(headsetEntity.Value, uid, headsetKeyHolder, component);
        }
    }

    private void CopyEncryptionKeys(EntityUid source, EntityUid target, EncryptionKeyHolderComponent sourceKeyHolder, EncryptionKeyHolderComponent targetKeyHolder)
    {
        // Clean the target container
        _container.CleanContainer(targetKeyHolder.KeyContainer);

        // Copy each key from source to target
        foreach (var key in sourceKeyHolder.KeyContainer.ContainedEntities.ToList())
        {
            var metaData = Comp<MetaDataComponent>(key);
            if (metaData.EntityPrototype?.ID is not { } prototypeId)
            {
                Log.Error($"Entity {ToPrettyString(key)} has no prototype ID in MetaDataComponent, cannot clone for encryption key copy.");
                continue;
            }
            var clonedKey = Spawn(prototypeId, Comp<TransformComponent>(target).Coordinates);

            if (!_container.Insert(clonedKey, targetKeyHolder.KeyContainer))
            {
                // Failed to insert, delete the cloned key
                QueueDel(clonedKey);
            }
        }
    }

    public void TryInsertEncryptionKey(EntityUid target, StartingGearPrototype startingGear)
    {
        TryInsertEncryptionKeyFromEquipmentSlot(target, "ears", startingGear.Equipment);
    }

    public void TryInsertEncryptionKey(EntityUid target, LoadoutPrototype loadout)
    {
        TryInsertEncryptionKeyFromEquipmentSlot(target, "ears", loadout.Equipment);
    }

    private void TryInsertEncryptionKeyFromEquipmentSlot(EntityUid target, string slotName, Dictionary<string, EntProtoId> equipment)
    {
        if (!TryComp<EncryptionKeyHolderComponent>(target, out var keyHolderComp)
            || !equipment.TryGetValue(slotName, out var earEquipProtoId)
            || string.IsNullOrEmpty(earEquipProtoId.Id))
            return;

        // Ensure earEquipProtoId.Id is not null or empty before spawning
        var earPrototypeId = earEquipProtoId.Id;
        if (string.IsNullOrEmpty(earPrototypeId))
        {
            Log.Warning($"Attempted to spawn item for slot '{slotName}' but EntProtoId had a null or empty ID.");
            return;
        }

        var earEntity = Spawn(earPrototypeId, Comp<TransformComponent>(target).Coordinates);

        if (!HasComp<EncryptionKeyHolderComponent>(earEntity)
            || !TryComp<ContainerFillComponent>(earEntity, out var fillComp)
            || !fillComp.Containers.TryGetValue(EncryptionKeyHolderComponent.KeyContainerName, out var defaultKeys))
        {
            QueueDel(earEntity);
            return;
        }

        _container.CleanContainer(keyHolderComp.KeyContainer);

        foreach (var keyProtoId in defaultKeys)
        {
            var keyToSpawn = keyProtoId;
            if (string.IsNullOrEmpty(keyToSpawn))
            {
                Log.Warning($"Empty key prototype ID found in ContainerFillComponent for {ToPrettyString(earEntity)}.");
                continue;
            }
            SpawnInContainerOrDrop(keyToSpawn, target, keyHolderComp.KeyContainer.ID);
        }
        QueueDel(earEntity);
    }
}
