using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.Storage;
using Robust.Shared.Containers;

namespace Content.Shared._Mono.ArmorPlate;

/// <summary>
/// Handles armor plate insertion, removal, and speed modifier application.
/// </summary>
public abstract class SharedArmorPlateSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmorPlateHolderComponent, EntInsertedIntoContainerMessage>(OnPlateInserted);
        SubscribeLocalEvent<ArmorPlateHolderComponent, EntRemovedFromContainerMessage>(OnPlateRemoved);
        SubscribeLocalEvent<ArmorPlateHolderComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ArmorPlateHolderComponent, InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshMoveSpeed);
    }

    private void OnPlateInserted(Entity<ArmorPlateHolderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != StorageComponent.ContainerId)
            return;

        var insertedEntity = args.Entity;

        if (!TryComp<ArmorPlateItemComponent>(insertedEntity, out var plateComp))
            return;

        var holder = ent.Comp;

        if (holder.ActivePlate == null)
        {
            SetActivePlate(ent, insertedEntity, plateComp, holder);
        }
    }

    private void OnPlateRemoved(Entity<ArmorPlateHolderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != StorageComponent.ContainerId)
            return;

        var removedEntity = args.Entity;
        var holder = ent.Comp;

        if (holder.ActivePlate != removedEntity)
            return;

        ClearActivePlate(ent, holder);

        if (TryComp<StorageComponent>(ent, out var storage))
        {
            foreach (var item in storage.Container.ContainedEntities)
            {
                if (TryComp<ArmorPlateItemComponent>(item, out var plateComp))
                {
                    SetActivePlate(ent, item, plateComp, holder);
                    break;
                }
            }
        }
    }

    private void OnExamined(Entity<ArmorPlateHolderComponent> ent, ref ExaminedEvent args)
    {
        var holder = ent.Comp;

        if (!TryComp<StorageComponent>(ent, out _))
        {
            args.PushMarkup(Loc.GetString("armor-plate-examine-no-storage"));
            return;
        }

        if (holder.ActivePlate == null)
        {
            args.PushMarkup(Loc.GetString("armor-plate-examine-no-plate"));
            return;
        }

        var plateName = MetaData(holder.ActivePlate.Value).EntityName;

        if (!TryComp<ArmorPlateItemComponent>(holder.ActivePlate.Value, out var plateItem))
        {
            args.PushMarkup(Loc.GetString("armor-plate-examine-with-plate-simple", ("plateName", plateName)));
            return;
        }

        if (TryComp<DamageableComponent>(holder.ActivePlate.Value, out var damageable))
        {
            var totalDamage = damageable.TotalDamage.Int();
            var maxDurability = plateItem.MaxDurability;

            var durabilityPercent = ((maxDurability - totalDamage) / (float)maxDurability) * 100f;
            durabilityPercent = Math.Clamp(durabilityPercent, 0f, 100f);

            var durabilityColor = durabilityPercent switch
            {
                > 66f => "green",
                >= 33f => "yellow",
                _ => "red",
            };

            args.PushMarkup(Loc.GetString("armor-plate-examine-with-plate",
                ("plateName", plateName),
                ("percent", (int)durabilityPercent),
                ("durabilityColor", durabilityColor)));
        }
        else
        {
            args.PushMarkup(Loc.GetString("armor-plate-examine-with-plate-simple", ("plateName", plateName)));
        }
    }

    private void OnRefreshMoveSpeed(EntityUid uid, ArmorPlateHolderComponent component, InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        args.Args.ModifySpeed(component.WalkSpeedModifier, component.SprintSpeedModifier);
    }

    /// <summary>
    /// Sets the active plate and updates speed modifiers.
    /// </summary>
    private void SetActivePlate(EntityUid holderUid, EntityUid plateUid, ArmorPlateItemComponent plateComp, ArmorPlateHolderComponent holder)
    {
        holder.ActivePlate = plateUid;
        holder.WalkSpeedModifier = plateComp.WalkSpeedModifier;
        holder.SprintSpeedModifier = plateComp.SprintSpeedModifier;
        holder.StaminaDamageMultiplier = plateComp.StaminaDamageMultiplier;

        Dirty(holderUid, holder);
        RefreshMovementSpeed(holderUid);
    }

    /// <summary>
    /// Clears the active plate and resets speed modifiers.
    /// </summary>
    private void ClearActivePlate(EntityUid holderUid, ArmorPlateHolderComponent holder)
    {
        holder.ActivePlate = null;
        holder.WalkSpeedModifier = 1.0f;
        holder.SprintSpeedModifier = 1.0f;
        holder.StaminaDamageMultiplier = 1.0f;

        Dirty(holderUid, holder);
        RefreshMovementSpeed(holderUid);
    }

    /// <summary>
    /// Refreshes movement speed for the entity wearing this armor.
    /// </summary>
    private void RefreshMovementSpeed(EntityUid armorUid)
    {
        if (_inventory.TryGetContainingEntity(armorUid, out var wearer))
        {
            _movementSpeed.RefreshMovementSpeedModifiers(wearer.Value);
        }
    }

    /// <summary>
    /// Tries to get the active plate from an armor holder.
    /// </summary>
    public bool TryGetActivePlate(Entity<ArmorPlateHolderComponent?> holder, out Entity<ArmorPlateItemComponent> plate)
    {
        plate = default;

        if (!Resolve(holder, ref holder.Comp, logMissing: false))
            return false;

        if (holder.Comp.ActivePlate == null)
            return false;

        if (!TryComp<ArmorPlateItemComponent>(holder.Comp.ActivePlate.Value, out var plateComp))
            return false;

        plate = (holder.Comp.ActivePlate.Value, plateComp);
        return true;
    }
}

