using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared.Administration.Logs;
using Content.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Weapons.Reflect;

/// <summary>
/// This handles reflecting projectiles and hitscan shots.
/// </summary>
public sealed class ReflectSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!; // WD EDIT

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReflectComponent, ProjectileReflectAttemptEvent>(OnReflectCollide);
        SubscribeLocalEvent<ReflectComponent, HitScanReflectAttemptEvent>(OnReflectHitscan);
        SubscribeLocalEvent<ReflectComponent, GotEquippedEvent>(OnReflectEquipped);
        SubscribeLocalEvent<ReflectComponent, GotUnequippedEvent>(OnReflectUnequipped);
        SubscribeLocalEvent<ReflectComponent, GotEquippedHandEvent>(OnReflectHandEquipped);
        SubscribeLocalEvent<ReflectComponent, GotUnequippedHandEvent>(OnReflectHandUnequipped);
        SubscribeLocalEvent<ReflectComponent, ItemToggledEvent>(OnToggleReflect);

        SubscribeLocalEvent<ReflectUserComponent, ProjectileReflectAttemptEvent>(OnReflectUserCollide);
        SubscribeLocalEvent<ReflectUserComponent, HitScanReflectAttemptEvent>(OnReflectUserHitscan);

        // Subscribe to inventory events to catch vest slot changes
        SubscribeLocalEvent<ReflectUserComponent, DidEquipEvent>(OnDidEquip);
        SubscribeLocalEvent<ReflectUserComponent, DidUnequipEvent>(OnDidUnequip);
    }

    private void OnReflectUserHitscan(EntityUid uid, ReflectUserComponent component, ref HitScanReflectAttemptEvent args)
    {
        if (args.Reflected)
            return;

        // Get all reflective items - from hands and vest slot
        var reflectiveItems = new List<(EntityUid Entity, ReflectComponent Component)>();

        // Check if the entity has hands component
        if (TryComp<HandsComponent>(uid, out var handsComp))
        {
            // Check items in hands
            foreach (var hand in handsComp.Hands.Values)
            {
                if (hand.HeldEntity == null)
                    continue;

                var ent = hand.HeldEntity.Value;
                if (TryComp<ReflectComponent>(ent, out var reflectComp) &&
                    _toggle.IsActivated((ent, null)) &&
                    (reflectComp.Reflects & args.Reflective) != 0x0)
                {
                    reflectiveItems.Add((ent, reflectComp));
                }
            }
        }

        // Check standard outerClothing slot (standard location for vests/armor)
        if (_inventorySystem.TryGetSlotEntity(uid, "outerClothing", out var outerEntity) &&
            outerEntity != null &&
            TryComp<ReflectComponent>(outerEntity.Value, out var outerReflectComp) &&
            _toggle.IsActivated((outerEntity.Value, null)) &&
            (outerReflectComp.Reflects & args.Reflective) != 0x0)
        {
            reflectiveItems.Add((outerEntity.Value, outerReflectComp));
        }

        // Fallback to "vest" slot
        if (_inventorySystem.TryGetSlotEntity(uid, "vest", out var vestEntity) &&
            vestEntity != null &&
            TryComp<ReflectComponent>(vestEntity.Value, out var vestReflectComp) &&
            _toggle.IsActivated((vestEntity.Value, null)) &&
            (vestReflectComp.Reflects & args.Reflective) != 0x0)
        {
            reflectiveItems.Add((vestEntity.Value, vestReflectComp));
        }

        // No reflective items found
        if (reflectiveItems.Count == 0)
            return;

        // Find the item with the highest reflection probability
        reflectiveItems.Sort((a, b) => b.Component.ReflectProb.CompareTo(a.Component.ReflectProb));
        var bestReflector = reflectiveItems[0];

        // Try to reflect with the best reflector
        if (TryReflectHitscan(uid, bestReflector.Entity, args.Shooter, args.SourceItem, args.Direction, args.Damage, out var dir))
        {
            args.Direction = dir.Value;
            args.Reflected = true;
        }
    }
    private void OnReflectUserCollide(EntityUid uid, ReflectUserComponent component, ref ProjectileReflectAttemptEvent args)
    {
        // First, check the projectile's reflective type
        if (!TryComp<ReflectiveComponent>(args.ProjUid, out var reflective))
            return;

        // Get all reflective items - from hands and vest slot
        var reflectiveItems = new List<(EntityUid Entity, ReflectComponent Component)>();

        // Check if the entity has hands component
        if (TryComp<HandsComponent>(uid, out var handsComp))
        {
            // Check items in hands
            foreach (var hand in handsComp.Hands.Values)
            {
                if (hand.HeldEntity == null)
                    continue;

                var ent = hand.HeldEntity.Value;
                if (TryComp<ReflectComponent>(ent, out var reflectComp) &&
                    _toggle.IsActivated((ent, null)) &&
                    (reflectComp.Reflects & reflective.Reflective) != 0x0)
                {
                    reflectiveItems.Add((ent, reflectComp));
                }
            }
        }

        // Check standard outerClothing slot (standard location for vests/armor)
        if (_inventorySystem.TryGetSlotEntity(uid, "outerClothing", out var outerEntity) &&
            outerEntity != null &&
            TryComp<ReflectComponent>(outerEntity.Value, out var outerReflectComp) &&
            _toggle.IsActivated((outerEntity.Value, null)) &&
            (outerReflectComp.Reflects & reflective.Reflective) != 0x0)
        {
            reflectiveItems.Add((outerEntity.Value, outerReflectComp));
        }

        // Fallback to "vest" slot
        if (_inventorySystem.TryGetSlotEntity(uid, "vest", out var vestEntity) &&
            vestEntity != null &&
            TryComp<ReflectComponent>(vestEntity.Value, out var vestReflectComp) &&
            _toggle.IsActivated((vestEntity.Value, null)) &&
            (vestReflectComp.Reflects & reflective.Reflective) != 0x0)
        {
            reflectiveItems.Add((vestEntity.Value, vestReflectComp));
        }

        // No reflective items found
        if (reflectiveItems.Count == 0)
            return;

        // Find the item with the highest reflection probability
        reflectiveItems.Sort((a, b) => b.Component.ReflectProb.CompareTo(a.Component.ReflectProb));
        var bestReflector = reflectiveItems[0];

        // Try to reflect with the best reflector
        if (TryReflectProjectile(uid, bestReflector.Entity, args.ProjUid, reflect: bestReflector.Component))
            args.Cancelled = true;
    }

    private void OnReflectCollide(EntityUid uid, ReflectComponent component, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (TryReflectProjectile(uid, uid, args.ProjUid, reflect: component))
            args.Cancelled = true;
    }

    private bool TryReflectProjectile(EntityUid user, EntityUid reflector, EntityUid projectile, ProjectileComponent? projectileComp = null, ReflectComponent? reflect = null)
    {
        if (!Resolve(reflector, ref reflect, false) ||
            !_toggle.IsActivated((reflector, null)) ||
            !TryComp<ReflectiveComponent>(projectile, out var reflective) ||
            (reflect.Reflects & reflective.Reflective) == 0x0 ||
            !_random.Prob(reflect.ReflectProb) ||
            !TryComp<PhysicsComponent>(projectile, out var physics))
        {
            return false;
        }

        var rotation = _random.NextAngle(-reflect.Spread / 2, reflect.Spread / 2).Opposite();
        var existingVelocity = _physics.GetMapLinearVelocity(projectile, component: physics);
        var relativeVelocity = existingVelocity - _physics.GetMapLinearVelocity(user);
        var newVelocity = rotation.RotateVec(relativeVelocity);

        // Have the velocity in world terms above so need to convert it back to local.
        var difference = newVelocity - existingVelocity;

        _physics.SetLinearVelocity(projectile, physics.LinearVelocity + difference, body: physics);

        var locRot = Transform(projectile).LocalRotation;
        var newRot = rotation.RotateVec(locRot.ToVec());
        _transform.SetLocalRotation(projectile, newRot.ToAngle());

        if (_netManager.IsServer)
        {
            _popup.PopupEntity(Loc.GetString("reflect-shot"), user);
            _audio.PlayPvs(reflect.SoundOnReflect, user, AudioHelpers.WithVariation(0.05f, _random));
        }

        if (Resolve(projectile, ref projectileComp, false))
        {
            // WD EDIT START
            if (reflect.DamageOnReflectModifier != 0)
            {
                _damageable.TryChangeDamage(reflector, projectileComp.Damage * reflect.DamageOnReflectModifier,
                    projectileComp.IgnoreResistances, origin: projectileComp.Shooter);
            }
            // WD EDIT END

            _adminLogger.Add(LogType.BulletHit, LogImpact.Medium, $"{ToPrettyString(user)} reflected {ToPrettyString(projectile)} from {ToPrettyString(projectileComp.Weapon)} shot by {projectileComp.Shooter}");

            projectileComp.Shooter = user;
            projectileComp.Weapon = user;
            Dirty(projectile, projectileComp);
        }
        else
        {
            _adminLogger.Add(LogType.BulletHit, LogImpact.Medium, $"{ToPrettyString(user)} reflected {ToPrettyString(projectile)}");
        }

        return true;
    }

    private void OnReflectHitscan(EntityUid uid, ReflectComponent component, ref HitScanReflectAttemptEvent args)
    {
        if (args.Reflected ||
            (component.Reflects & args.Reflective) == 0x0)
        {
            return;
        }

        if (TryReflectHitscan(uid, uid, args.Shooter, args.SourceItem, args.Direction, args.Damage, out var dir)) // WD EDIT
        {
            args.Direction = dir.Value;
            args.Reflected = true;
        }
    }

    private bool TryReflectHitscan(
        EntityUid user,
        EntityUid reflector,
        EntityUid? shooter,
        EntityUid shotSource,
        Vector2 direction,
        DamageSpecifier? damage, // WD EDIT
        [NotNullWhen(true)] out Vector2? newDirection)
    {
        if (!TryComp<ReflectComponent>(reflector, out var reflect) ||
            !_toggle.IsActivated((reflector, null)) ||
            !_random.Prob(reflect.ReflectProb))
        {
            newDirection = null;
            return false;
        }

        if (_netManager.IsServer)
        {
            _popup.PopupEntity(Loc.GetString("reflect-shot"), user);
            _audio.PlayPvs(reflect.SoundOnReflect, user, AudioHelpers.WithVariation(0.05f, _random));
        }

        // WD EDIT START
        if (reflect.DamageOnReflectModifier != 0 && damage != null)
            _damageable.TryChangeDamage(reflector, damage * reflect.DamageOnReflectModifier, origin: shooter);
        // WD EDIT END

        var spread = _random.NextAngle(-reflect.Spread / 2, reflect.Spread / 2);
        newDirection = -spread.RotateVec(direction);

        if (shooter != null)
            _adminLogger.Add(LogType.HitScanHit, LogImpact.Medium, $"{ToPrettyString(user)} reflected hitscan from {ToPrettyString(shotSource)} shot by {ToPrettyString(shooter.Value)}");
        else
            _adminLogger.Add(LogType.HitScanHit, LogImpact.Medium, $"{ToPrettyString(user)} reflected hitscan from {ToPrettyString(shotSource)}");

        return true;
    }

    private void OnReflectEquipped(EntityUid uid, ReflectComponent component, GotEquippedEvent args)
    {
        if (_gameTiming.ApplyingState)
            return;

        EnsureComp<ReflectUserComponent>(args.Equipee);
    }

    private void OnReflectUnequipped(EntityUid uid, ReflectComponent comp, GotUnequippedEvent args)
    {
        RefreshReflectUser(args.Equipee);
    }

    private void OnReflectHandEquipped(EntityUid uid, ReflectComponent component, GotEquippedHandEvent args)
    {
        if (_gameTiming.ApplyingState)
            return;

        EnsureComp<ReflectUserComponent>(args.User);
    }

    private void OnReflectHandUnequipped(EntityUid uid, ReflectComponent component, GotUnequippedHandEvent args)
    {
        RefreshReflectUser(args.User);
    }

    private void OnToggleReflect(EntityUid uid, ReflectComponent comp, ref ItemToggledEvent args)
    {
        if (args.User is {} user)
            RefreshReflectUser(user);
    }

    private void OnDidEquip(EntityUid uid, ReflectUserComponent component, DidEquipEvent args)
    {
        // We only care if we're the equipee
        if (args.Equipee == uid)
            RefreshReflectUser(uid);
    }

    private void OnDidUnequip(EntityUid uid, ReflectUserComponent component, DidUnequipEvent args)
    {
        // We only care if we're the equipee
        if (args.Equipee == uid)
            RefreshReflectUser(uid);
    }

    /// <summary>
    /// Refreshes whether someone has reflection potential so we can raise directed events on them.
    /// </summary>
    private void RefreshReflectUser(EntityUid user)
    {
        bool hasReflectItem = false;

        // Check if the entity has hands component
        if (TryComp<HandsComponent>(user, out var handsComp))
        {
            // Check items in hands
            foreach (var hand in handsComp.Hands.Values)
            {
                if (hand.HeldEntity == null)
                    continue;

                var ent = hand.HeldEntity.Value;
                if (HasComp<ReflectComponent>(ent) && _toggle.IsActivated((ent, null)))
                {
                    hasReflectItem = true;
                    break;
                }
            }
        }

        // Check the vest slot - try both "vest" and "outerClothing" which is the standard name
        if (!hasReflectItem)
        {
            // Try standard "outerClothing" slot first
            if (_inventorySystem.TryGetSlotEntity(user, "outerClothing", out var outerEntity) &&
                outerEntity != null &&
                HasComp<ReflectComponent>(outerEntity.Value) &&
                _toggle.IsActivated((outerEntity.Value, null)))
            {
                hasReflectItem = true;
            }
            // Fallback to "vest" slot if the first check fails
            else if (_inventorySystem.TryGetSlotEntity(user, "vest", out var vestEntity) &&
                vestEntity != null &&
                HasComp<ReflectComponent>(vestEntity.Value) &&
                _toggle.IsActivated((vestEntity.Value, null)))
            {
                hasReflectItem = true;
            }
        }

        if (hasReflectItem)
            EnsureComp<ReflectUserComponent>(user);
        else
            RemCompDeferred<ReflectUserComponent>(user);
    }
}

