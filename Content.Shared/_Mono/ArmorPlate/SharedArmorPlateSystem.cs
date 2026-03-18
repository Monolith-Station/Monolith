using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Mono.ArmorPlate;

/// <summary>
/// Handles all armor plate behavior
/// </summary>
public sealed class SharedArmorPlateSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmorPlateHolderComponent, EntInsertedIntoContainerMessage>(OnPlateInserted);
        SubscribeLocalEvent<ArmorPlateHolderComponent, EntRemovedFromContainerMessage>(OnPlateRemoved);
        SubscribeLocalEvent<ArmorPlateHolderComponent, GotEquippedEvent>(OnEquippedArmor);
        SubscribeLocalEvent<ArmorPlateHolderComponent, GotUnequippedEvent>(OnUnequippedArmor);
        SubscribeLocalEvent<ArmorPlateHolderComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ArmorPlateHolderComponent, InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<ArmorPlateItemComponent, GetVerbsEvent<ExamineVerb>>(OnPlateVerbExamine);
        SubscribeLocalEvent<ArmorPlateItemComponent, EntityTerminatingEvent>(OnPlateDestroyed);
        SubscribeLocalEvent<ArmorPlateItemComponent, ExaminedEvent>(OnPlateExamined);
        SubscribeLocalEvent<ArmorPlateProtectedComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
    }

    public void OnBeforeDamageChanged(Entity<ArmorPlateProtectedComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled || !args.Damage.AnyPositive())
            return;

        if (!TryComp<InventoryComponent>(ent.Owner, out var inv))
            return;

        if (!_inventory.TryGetSlots(ent, out var slots))
            return;

        if (args.Origin == null && args.OriginFlag != DamageableSystem.DamageOriginFlag.Explosion)
            return;

        foreach (var slot in slots)
        {
            if (!_inventory.TryGetSlotEntity(ent, slot.Name, out var equipped, inv))
                continue;

            if (!TryComp<ArmorPlateHolderComponent>(equipped, out var holder))
                continue;

            if (!TryGetActivePlate((equipped.Value, holder), out var plate))
                continue;

            // Calculate damages owed to plate and holder
            CalcPlateDamages(args.Damage, plate.Comp, out var remainder, out var absorbed, out var plateDamage);

            // Damage to plate, stamina damage to holder
            AbsorbDamage(ent, equipped.Value, holder, plate, absorbed, plateDamage);

            // Full absorption, done
            if (remainder.Empty)
            {
                args.Cancelled = true;
                return;
            }

            // Replace raw damage with remaining damage post-absorption
            args.Damage.DamageDict.Clear();
            foreach (var (type, amt) in remainder.DamageDict)
                args.Damage.DamageDict.Add(type, amt);
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
        RefreshPlateProtection(holderUid);
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
        RefreshPlateProtection(holderUid);
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

    /// <summary>
    /// Calculate numbers used for damaging plate and player
    /// </summary>
    public void CalcPlateDamages(DamageSpecifier incoming, ArmorPlateItemComponent plate, out DamageSpecifier remainder, out FixedPoint2 absorbedTotal, out FixedPoint2 plateDamageTotal)
    {
        remainder = new DamageSpecifier();
        absorbedTotal = FixedPoint2.Zero;
        plateDamageTotal = FixedPoint2.Zero;

        foreach (var (type, amount) in incoming.DamageDict)
        {
            if (amount <= FixedPoint2.Zero)
                continue;

            var multiplier = plate.DamageMultipliers.GetValueOrDefault(type, 1.0f);
            var ratio = plate.AbsorptionRatios.GetValueOrDefault(type, 0f);

            FixedPoint2 absorbed = FixedPoint2.Zero;
            FixedPoint2 remainderAmt = amount;

            if (ratio > 0f)
            {
                absorbed = amount * ratio;
                remainderAmt = amount - absorbed;
            }
            else if (ratio < 0f)
            {
                remainderAmt = amount * (1f + Math.Abs(ratio));
            }

            var plateDamage = amount * Math.Abs(ratio) * multiplier;

            absorbedTotal = absorbedTotal + absorbed;
            plateDamageTotal = plateDamageTotal + plateDamage;

            if (remainderAmt > FixedPoint2.Zero)
                remainder.DamageDict.Add(type, remainderAmt);
        }
    }

    /// <summary>
    /// Examine tooltip handler
    /// </summary>
    private void OnPlateVerbExamine(EntityUid uid, ArmorPlateItemComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var examineMarkup = GetPlateExamine(component);

        var ev = new ArmorExamineEvent(examineMarkup);
        RaiseLocalEvent(uid, ref ev);

        _examine.AddDetailedExamineVerb(args, component, examineMarkup,
            Loc.GetString("armor-plate-examinable-verb-text"),
            "/Textures/Interface/VerbIcons/dot.svg.192dpi.png",
            Loc.GetString("armor-plate-examinable-verb-message"));
    }

    // Used to tell the .ftl if it's a positive or negative value
    private static int CalcDirection(float ratio) => ratio < 0 ? 1 : ratio > 0 ? -1 : 0;
    //Speed tooltip generating method
    private void AddSpeedDisplay(FormattedMessage msg, string gaitType, float speedCalc)
    {
        var deltaSign = CalcDirection(speedCalc);

        msg.PushNewline();
        msg.AddMarkupOrThrow(Loc.GetString("armor-plate-speed-display",
            ("gait", gaitType),
            ("deltasign", deltaSign),
            ("speedPercent", Math.Abs(speedCalc))
        ));
    }

    private FormattedMessage GetPlateExamine(ArmorPlateItemComponent plate)
    {
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("armor-plate-attributes-examine"));

        msg.PushNewline();

        msg.AddMarkupOrThrow(Loc.GetString("armor-plate-initial-durability",
            ("durability", plate.MaxDurability)
        ));

        var walkModifierCalc = MathF.Round((plate.WalkSpeedModifier - 1.0f) * 100f, 1);
        var sprintModifierCalc = MathF.Round((plate.SprintSpeedModifier - 1.0f) * 100f, 1);

        if (!(walkModifierCalc == 0.0f && sprintModifierCalc == 0.0f))
        {
            if (MathHelper.CloseTo(walkModifierCalc, sprintModifierCalc, 0.5f))
            {
                AddSpeedDisplay(msg, Loc.GetString("armor-plate-gait-speed"), walkModifierCalc);
            }
            else
            {
                AddSpeedDisplay(msg, Loc.GetString("armor-plate-gait-sprint"), sprintModifierCalc);
                AddSpeedDisplay(msg, Loc.GetString("armor-plate-gait-walk"), walkModifierCalc);
            }
        }

        foreach (var kv in plate.AbsorptionRatios)
        {
            msg.PushNewline();

            var dmgType = Loc.GetString("armor-damage-type-" + kv.Key.ToLower());
            var ratioPercent = MathF.Round(kv.Value * 100, 1);

            var multiplier = plate.DamageMultipliers.GetValueOrDefault(kv.Key, 1.0f);
            var multiplierStr = multiplier.ToString("0.##");
            var deltaSign = CalcDirection(kv.Value);

            msg.AddMarkupOrThrow(Loc.GetString("armor-plate-ratios-display",
                ("deltasign", deltaSign),
                ("dmgType", dmgType),
                ("ratioPercent", Math.Abs(ratioPercent)),
                ("multiplier", multiplierStr)
            ));
        }

        msg.PushNewline();
        var staminaPercent = MathF.Round(plate.StaminaDamageMultiplier * 100f, 1);
        msg.AddMarkupOrThrow(Loc.GetString("armor-plate-stamina-value",
            ("multiplier", staminaPercent)));

        return msg;
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

    /// <summary>
    /// Starts listening to damage instances for plate evaluation on equip of a plate-bearing item.
    /// </summary>
    private void OnEquippedArmor(Entity<ArmorPlateHolderComponent> armor, ref GotEquippedEvent args)
    {
        if (TryGetActivePlate((armor.Owner, armor.Comp), out _))
        {
            EnsureComp<ArmorPlateProtectedComponent>(args.Equipee);
        }
    }

    /// <summary>
    /// Stops listening to damage instances for plate evaluation on unequip.
    /// </summary>
    private void OnUnequippedArmor(Entity<ArmorPlateHolderComponent> armor, ref GotUnequippedEvent args)
    {
        if (TryGetActivePlate((armor.Owner, armor.Comp), out _))
        {
            RemComp<ArmorPlateProtectedComponent>(args.Equipee);
        }
    }

    /// <summary>
    /// Re-evaluates plate holder status.
    /// </summary>
    private void RefreshPlateProtection(EntityUid armorUid)
    {
        if (!_inventory.TryGetContainingEntity(armorUid, out var wearer))
            return;

        var wearerUid = wearer.Value;

        if (!TryComp<ArmorPlateHolderComponent>(armorUid, out var holder))
            return;

        if (TryGetActivePlate((armorUid, holder), out _))
            EnsureComp<ArmorPlateProtectedComponent>(wearerUid);
        else
            RemComp<ArmorPlateProtectedComponent>(wearerUid);
    }
    private void OnPlateExamined(EntityUid uid, ArmorPlateItemComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (TryComp<DamageableComponent>(uid, out var damageable))
        {
            var totalDamage = damageable.TotalDamage.Int();
            var maxDurability = component.MaxDurability;
            var durabilityPercent = ((maxDurability - totalDamage) / (float)maxDurability) * 100f;
            durabilityPercent = Math.Clamp(durabilityPercent, 0f, 100f);

            var durabilityColor = durabilityPercent switch
            {
                > 66f => "green",
                >= 33f => "yellow",
                _ => "red",
            };

            args.PushMarkup(Loc.GetString("armor-plate-item-durability",
                ("percent", (int)durabilityPercent),
                ("durabilityColor", durabilityColor)));
        }
    }
}
