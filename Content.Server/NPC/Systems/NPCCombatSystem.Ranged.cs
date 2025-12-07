using System.Numerics;
using Content.Server.NPC.Components;
using Content.Shared._Goobstation.Weapons.SmartGun;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Components;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server.NPC.Systems;

public sealed partial class NPCCombatSystem
{
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly RotateToFaceSystem _rotate = default!;
    [Dependency] private readonly SharedLaserPointerSystem _pointer = default!; // Goobstation

    private EntityQuery<CombatModeComponent> _combatQuery;
    private EntityQuery<NPCSteeringComponent> _steeringQuery;
    private EntityQuery<RechargeBasicEntityAmmoComponent> _rechargeQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<RequireProjectileTargetComponent> _requireTargetQuery; // Mono

    // TODO: Don't predict for hitscan
    private const float ShootSpeed = 20f;

    /// <summary>
    /// Cooldown on raycasting to check LOS.
    /// </summary>
    public const float UnoccludedCooldown = 0.2f;

    private void InitializeRanged()
    {
        _combatQuery = GetEntityQuery<CombatModeComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _rechargeQuery = GetEntityQuery<RechargeBasicEntityAmmoComponent>();
        _steeringQuery = GetEntityQuery<NPCSteeringComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _requireTargetQuery = GetEntityQuery<RequireProjectileTargetComponent>(); // Mono

        SubscribeLocalEvent<NPCRangedCombatComponent, ComponentStartup>(OnRangedStartup);
        SubscribeLocalEvent<NPCRangedCombatComponent, ComponentShutdown>(OnRangedShutdown);
    }

    private void OnRangedStartup(EntityUid uid, NPCRangedCombatComponent component, ComponentStartup args)
    {
        if (TryComp<CombatModeComponent>(uid, out var combat))
        {
            _combat.SetInCombatMode(uid, true, combat);
        }
        else
        {
            component.Status = CombatStatus.Unspecified;
        }
    }

    private void OnRangedShutdown(EntityUid uid, NPCRangedCombatComponent component, ComponentShutdown args)
    {
        if (TryComp<CombatModeComponent>(uid, out var combat))
        {
            _combat.SetInCombatMode(uid, false, combat);
        }

        // Goobstation
        if (_gun.TryGetGun(uid, out var gunUid, out _) && TryComp(gunUid, out LaserPointerComponent? laser) &&
            TryComp(gunUid, out WieldableComponent? wieldable) && TryComp(gunUid, out TransformComponent? xform))
            _pointer.AddOrRemoveLine(GetNetEntity(gunUid), laser, wieldable, xform, null, null);
    }

    private void UpdateRanged(float frameTime)
    {
        var query = EntityQueryEnumerator<NPCRangedCombatComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!_gun.TryGetGun(uid, out var gunUid, out var gun))
            {
                comp.Status = CombatStatus.NoWeapon;
                comp.ShootAccumulator = 0f;
                continue;
            }

            if (comp.Status == CombatStatus.Unspecified)
            {
                UpdatePointerLineNoTarget(gunUid); // Goobstation
                continue;
            }

            if (_steeringQuery.TryGetComponent(uid, out var steering) && steering.Status == SteeringStatus.NoPath)
            {
                comp.Status = CombatStatus.TargetUnreachable;
                comp.ShootAccumulator = 0f;
                UpdatePointerLineNoTarget(gunUid); // Goobstation
                continue;
            }

            if (!_xformQuery.TryGetComponent(comp.Target, out var targetXform) ||
                !_physicsQuery.TryGetComponent(comp.Target, out var targetBody))
            {
                comp.Status = CombatStatus.TargetUnreachable;
                comp.ShootAccumulator = 0f;
                UpdatePointerLineNoTarget(gunUid); // Goobstation
                continue;
            }

            if (targetXform.MapID != xform.MapID)
            {
                comp.Status = CombatStatus.TargetUnreachable;
                comp.ShootAccumulator = 0f;
                continue;
            }

            if (_combatQuery.TryGetComponent(uid, out var combatMode))
            {
                _combat.SetInCombatMode(uid, true, combatMode);
            }

            var ammoEv = new GetAmmoCountEvent();
            RaiseLocalEvent(gunUid, ref ammoEv);

            var worldPos = _transform.GetWorldPosition(xform);
            var targetPos = _transform.GetWorldPosition(targetXform);

            if (ammoEv.Count == 0)
            {
                // Recharging then?
                if (_rechargeQuery.HasComponent(gunUid))
                {
                    UpdatePointerLine(gunUid, targetPos, worldPos, comp.Target); // Goobstation
                    continue;
                }

                comp.Status = CombatStatus.Unspecified;
                comp.ShootAccumulator = 0f;
                UpdatePointerLine(gunUid, targetPos, worldPos, comp.Target); // Goobstation
                continue;
            }

            comp.LOSAccumulator -= frameTime;

            // We'll work out the projected spot of the target and shoot there instead of where they are.
            var distance = (targetPos - worldPos).Length();
            var oldInLos = comp.TargetInLOS;

            // TODO: Should be doing these raycasts in parallel
            // Ideally we'd have 2 steps, 1. to go over the normal details for shooting and then 2. to handle beep / rotate / shoot
            if (comp.LOSAccumulator < 0f)
            {
                comp.LOSAccumulator += UnoccludedCooldown;
                // For consistency with NPC steering.                                                  // Mono
                comp.TargetInLOS = _interaction.InRangeUnobstructed(uid, comp.Target, distance + 0.1f, comp.ObstructedMask, predicate: (EntityUid entity) =>
                {
                    return _physicsQuery.TryGetComponent(entity, out var physics) && (physics.CollisionLayer & (int)comp.BulletMask) == 0 // ignore if it can't collide with bullets
                        || _requireTargetQuery.HasComponent(entity); // or if it requires targeting
                });
            }

            if (!comp.TargetInLOS)
            {
                comp.ShootAccumulator = 0f;
                comp.Status = CombatStatus.NotInSight;

                if (TryComp(uid, out steering))
                {
                    steering.ForceMove = true;
                }

                UpdatePointerLine(gunUid, targetPos, worldPos, comp.Target); // Goobstation
                continue;
            }

            if (!oldInLos && comp.SoundTargetInLOS != null)
            {
                _audio.PlayPvs(comp.SoundTargetInLOS, uid);
            }

            comp.ShootAccumulator += frameTime;

            if (comp.ShootAccumulator < comp.ShootDelay)
            {
                UpdatePointerLine(gunUid, targetPos, worldPos, comp.Target); // Goobstation
                continue;
            }

            var mapVelocity = targetBody.LinearVelocity;
            var targetSpot = targetPos + mapVelocity * distance / ShootSpeed;

            // If we have a max rotation speed then do that.
            var goalRotation = (targetSpot - worldPos).ToWorldAngle();
            var rotationSpeed = comp.RotationSpeed;

            if (!_rotate.TryRotateTo(uid, goalRotation, frameTime, comp.AccuracyThreshold, rotationSpeed?.Theta ?? double.MaxValue, xform))
            {
                UpdatePointerLine(gunUid, targetPos, worldPos, comp.Target); // Goobstation
                continue;
            }

            // TODO: LOS
            // TODO: Ammo checks
            // TODO: Burst fire
            // TODO: Cycling
            // Max rotation speed

            // TODO: Check if we can face

            if (!Enabled || !_gun.CanShoot(gun))
            {
                UpdatePointerLine(gunUid, targetPos, worldPos, comp.Target); // Goobstation
                continue;
            }

            EntityCoordinates targetCordinates;

            if (_mapManager.TryFindGridAt(xform.MapID, targetPos, out var gridUid, out var mapGrid))
            {
                targetCordinates = new EntityCoordinates(gridUid, mapGrid.WorldToLocal(targetSpot));
            }
            else
            {
                targetCordinates = new EntityCoordinates(xform.MapUid!.Value, targetSpot);
            }

            comp.Status = CombatStatus.Normal;

            UpdatePointerLine(gunUid, targetPos, worldPos, comp.Target); // Goobstation

            if (gun.NextFire > _timing.CurTime)
            {
                return;
            }

            _gun.SetTarget(gun, comp.Target); // Frontier - This ensures that the bullet won't fly over the target if it's downed
            _gun.AttemptShoot(uid, gunUid, gun, targetCordinates);
        }
    }

    // Goobstation - Helper methods moved outside the loop to avoid redefinition
    private void UpdatePointerLineNoTarget(EntityUid gunUid)
    {
        if (TryComp(gunUid, out LaserPointerComponent? pointer) && TryComp(gunUid, out WieldableComponent? wieldable))
        {
            _pointer.AddOrRemoveLine(GetNetEntity(gunUid),
                pointer,
                wieldable,
                _xformQuery.Comp(gunUid),
                null,
                null);
        }
    }

    private void UpdatePointerLine(EntityUid gunUid, Vector2 targetPos, Vector2 worldPos, EntityUid target)
    {
        if (TryComp(gunUid, out LaserPointerComponent? pointer) && TryComp(gunUid, out WieldableComponent? wieldable))
        {
            _pointer.AddOrRemoveLine(GetNetEntity(gunUid),
                pointer,
                wieldable,
                _xformQuery.Comp(gunUid),
                targetPos - worldPos,
                target);
        }
    }
}
