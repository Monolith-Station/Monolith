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
using System.Collections;

namespace Content.Shared._Mono.ArmorPlate;

/// <summary>
/// Handles all armor plate behavior
/// </summary>
public sealed partial class SharedArmorPlateSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private StaminaSystem _stamina = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedContainerSystem _container = default!;

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

            // Calculate damages owed to plate and holder, then apply damage to plate and stamina damage to holder.
            CalcPlateDamages(args.Damage, plate.Comp, out var remainder, out var absorbed, out var plateDamage);

            if (plate.Comp.MaxDurability != -1)
                DamagePlate(ent, equipped.Value, holder, plate, plateDamage);

            if (plate.Comp.StaminaDamageMultipliers.Count > 0)
            {
                InflictStamina(ent, args.Damage, absorbed, remainder, plate.Comp.StaminaDamageMultipliers);
            }

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

    private void DamagePlate(
        EntityUid wearer,
        EntityUid armorUid,
        ArmorPlateHolderComponent holder,
        Entity<ArmorPlateItemComponent> plate,
        FixedPoint2 plateDamage)

    {
        var damageSpec = new DamageSpecifier();
        damageSpec.DamageDict.Add("Blunt", plateDamage);

        _damageable.TryChangeDamage(plate.Owner, damageSpec, ignoreResistances: true);
    }

    private void InflictStamina(
        EntityUid wearer,
        DamageSpecifier rawDamage,
        FixedPoint2 absorbed,
        DamageSpecifier remainder,
        Dictionary<string, float> multipliers)
    {
        float staminaDamage = 0f;

        //If raw flag is present, it overrides to prevent double dipping
        if (multipliers.TryGetValue("Raw", out var rawMult))
        {
            foreach (var (type, amt) in rawDamage.DamageDict)
                if (type != "Structural")
                    staminaDamage += amt.Float() * rawMult;
        }
        else
        {
            //Absorbed, pretty straightforward
            if (multipliers.TryGetValue("Absorbed", out var absorbMult))
                staminaDamage += absorbed.Float() * absorbMult;

            //Amplified = Remainder - Raw
            if (multipliers.TryGetValue("Amplified", out var amplifyMult))
            {
                float amplified = 0f;

                foreach (var (_, amt) in remainder.DamageDict)
                    amplified += amt.Float();

                foreach (var (_, amt) in rawDamage.DamageDict)
                    amplified -= amt.Float();

                staminaDamage += MathF.Max(0f, amplified) * amplifyMult;
            }
        }

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
        holder.ActiveStaminaMultipliers = new Dictionary<string, float>(plateComp.StaminaDamageMultipliers);

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
        holder.ActiveStaminaMultipliers.Clear();

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

            var multiplier = plate.DamageToPlate.GetValueOrDefault(type, 0f);
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

            var plateDamage = amount * multiplier;

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

    //Speed tooltip generating method
    private void AddSpeedDisplay(FormattedMessage msg, string gaitType, float speedCalc)
    {
        var stringClause = MathF.Sign(speedCalc);

        msg.PushNewline();
        msg.AddMarkupOrThrow(Loc.GetString("armor-plate-speed-display",
            ("gait", gaitType),
            ("stringClause", stringClause),
            ("speedPercent", Math.Abs(speedCalc))
        ));
    }

    private FormattedMessage GetPlateExamine(ArmorPlateItemComponent plate)
    {
        //Examine header
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("armor-plate-attributes-examine"));

        //Durability info (if plate can break)
        if (plate.MaxDurability != -1)
        {
            msg.PushNewline();
            msg.AddMarkupOrThrow(Loc.GetString("armor-plate-initial-durability",
                ("durability", plate.MaxDurability)
            ));
        }

        //Speed (if it is affected)
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

        foreach (var (type, ratio) in plate.AbsorptionRatios)
        {
            //Damage absorption per type
            msg.PushNewline();

            var dmgType = Loc.GetString("armor-damage-type-" + type.ToLower());
            var ratioPercent = MathF.Round(ratio * 100, 1);

            var stringClause = MathF.Sign(ratio);

            msg.AddMarkupOrThrow(Loc.GetString("armor-plate-ratios-display",
                ("stringClause", stringClause),
                ("dmgType", dmgType),
                ("ratioPercent", Math.Abs(ratioPercent))
            ));

            //Append damagetoplate information to the current absorption line (if plate can break)
            var multiplier = plate.DamageToPlate.GetValueOrDefault(type, 0f);

            if (plate.MaxDurability == -1)
                continue;

            if(multiplier > 0)
            {
                var multiplierPercent = MathF.Round(multiplier * 100, 1);
                msg.AddMarkupOrThrow(" " + Loc.GetString("armor-plate-multiplier-display",
                    ("multiplier", multiplierPercent),
                    ("dmgType", dmgType)));
            }
            else
            {
                msg.AddMarkupOrThrow(" " + Loc.GetString("armor-plate-multiplier-none"));
            }
        }

        //Stamina damage (if it can inflict any)

        //Raw
        if (plate.StaminaDamageMultipliers.TryGetValue("Raw", out var rawMultiplier) && rawMultiplier > 0f)
        {
            msg.PushNewline();
            var staminaPercent = MathF.Round(rawMultiplier * 100f, 1);
            var localizedSource = Loc.GetString("armor-plate-stamina-source-raw");

            msg.AddMarkupOrThrow(Loc.GetString("armor-plate-stamina-value",
                ("multiplier", staminaPercent),
                ("sources", localizedSource)));
        }

        //Absorbed & Amplified
        else
        {
            var absorbedPercent = 0f;
            var amplifiedPercent = 0f;

            if (plate.StaminaDamageMultipliers.TryGetValue("Absorbed", out var absorbedMultiplier) && absorbedMultiplier > 0f)
            {
                absorbedPercent = MathF.Round(absorbedMultiplier * 100f, 1);
            }

            if (plate.StaminaDamageMultipliers.TryGetValue("Amplified", out var amplifiedMultiplier) && amplifiedMultiplier > 0f)
            {
                amplifiedPercent = MathF.Round(amplifiedMultiplier * 100f, 1);
            }

            //Seperate incongruent values
            if (absorbedPercent != amplifiedPercent)
            {
                if (absorbedPercent > 0)
                {
                    msg.PushNewline();
                    msg.AddMarkupOrThrow(Loc.GetString("armor-plate-stamina-value",
                        ("multiplier", absorbedPercent),
                        ("sources", Loc.GetString("armor-plate-stamina-source-absorb"))));
                }

                if (amplifiedPercent > 0)
                {
                    msg.PushNewline();
                    msg.AddMarkupOrThrow(Loc.GetString("armor-plate-stamina-value",
                        ("multiplier", amplifiedPercent),
                        ("sources", Loc.GetString("armor-plate-stamina-source-amplified"))));
                }
            }

            //Print together if absorbed = amplified and not 0
            else if (absorbedPercent > 0 & amplifiedPercent > 0)
            {
                var sourceString = $"{Loc.GetString("armor-plate-stamina-source-absorb")}" + " " +
                    $"{Loc.GetString("armor-plate-stamina-concat")}" + " " +
                    $"{Loc.GetString("armor-plate-stamina-source-amplified")}";

                msg.PushNewline();
                msg.AddMarkupOrThrow(Loc.GetString("armor-plate-stamina-value",
                    ("multiplier", absorbedPercent),
                    ("sources", sourceString)));
            }
        }

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
