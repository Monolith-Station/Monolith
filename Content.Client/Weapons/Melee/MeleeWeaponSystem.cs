using System.Diagnostics.CodeAnalysis; // Mono - dual wielding
using System.Linq;
using Content.Client.Gameplay;
using Content.Shared._Mono.DualWield; // Mono - dual wielding
using Content.Shared._White.Blink;
using Content.Shared.CombatMode;
using Content.Shared.Effects;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Wieldable.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client.Weapons.Melee;

public sealed partial class MeleeWeaponSystem : SharedMeleeWeaponSystem
{
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IInputManager _inputManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IStateManager _stateManager = default!;
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private InputSystem _inputSystem = default!;
    [Dependency] private SharedColorFlashEffectSystem _color = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private TransformSystem _transform = default!; // Goobstation
    [Dependency] private SharedDualWieldSystem _dualWield = default!; // Mono - dual wielding

    private EntityQuery<TransformComponent> _xformQuery;

    private const string MeleeLungeKey = "melee-lunge";

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
        SubscribeNetworkEvent<MeleeLungeEvent>(OnMeleeLunge);
        UpdatesOutsidePrediction = true;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        UpdateEffects();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return;

        var entityNull = _player.LocalEntity;

        if (entityNull == null)
            return;

        var entity = entityNull.Value;

        // Mono - dual wielding
        if (TryComp<DualWieldComponent>(entity, out var dualWield))
        {
            UpdateDualWield((entity, dualWield));
            return;
        }
        // End Mono

        // Mono - add user override
        if (!TryGetWeapon(entity, out var weaponUid, out var weapon, out var userOverride))
            return;

        if (!CombatMode.IsInCombatMode(entity) || !Blocker.CanAttack(userOverride, weapon: (weaponUid, weapon)))
        {
            weapon.Attacking = false;
            return;
        }

        var useDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use);
        var altDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.UseSecondary);

        if (weapon.AutoAttack || useDown != BoundKeyState.Down && altDown != BoundKeyState.Down)
        {
            if (weapon.Attacking)
            {
                RaisePredictiveEvent(new StopAttackEvent(GetNetEntity(weaponUid)));
            }
        }

        if (weapon.Attacking || weapon.NextAttack > Timing.CurTime)
        {
            return;
        }

        // TODO using targeted actions while combat mode is enabled should NOT trigger attacks.

        var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);

        if (mousePos.MapId == MapId.Nullspace)
        {
            return;
        }

        EntityCoordinates coordinates;

        if (MapManager.TryFindGridAt(mousePos, out var gridUid, out _))
        {
            coordinates = TransformSystem.ToCoordinates(gridUid, mousePos);
        }
        else
        {
            coordinates = TransformSystem.ToCoordinates(_map.GetMap(mousePos.MapId), mousePos);
        }

        // If the gun has AltFireComponent, it can be used to attack.
        if (TryComp<GunComponent>(weaponUid, out var gun) && gun.UseKey)
        {
            if (!TryComp<AltFireMeleeComponent>(weaponUid, out var altFireComponent) || altDown != BoundKeyState.Down)
                return;

            switch(altFireComponent.AttackType)
            {
                case AltFireAttackType.Light:
                    ClientLightAttack(userOverride, mousePos, coordinates, weaponUid, weapon);
                    break;

                case AltFireAttackType.Heavy:
                    ClientHeavyAttack(userOverride, coordinates, weaponUid, weapon);
                    break;

                case AltFireAttackType.Disarm:
                    ClientDisarm(userOverride, mousePos, coordinates);
                    break;
            }

            return;
        }

        // Heavy attack.
        if (altDown == BoundKeyState.Down)
        {
            // If it's an unarmed attack then do a disarm
            if (weapon.AltDisarm && weaponUid == userOverride)
            {
                EntityUid? target = null;

                if (_stateManager.CurrentState is GameplayStateBase screen)
                {
                    target = screen.GetDamageableClickedEntity(mousePos); // Goob edit
                }

                EntityManager.RaisePredictiveEvent(new DisarmAttackEvent(GetNetEntity(target), GetNetCoordinates(coordinates)));
                return;
            }

            // WD EDIT START
            if (TryComp(weaponUid, out BlinkComponent? blink) && blink.IsActive)
            {
                if (!_xformQuery.TryGetComponent(userOverride, out var userXform))
                    return;

                var targetMap = _transform.ToMapCoordinates(coordinates);

                if (targetMap.MapId != userXform.MapID)
                    return;

                var userPos = TransformSystem.GetWorldPosition(userXform);
                var direction = targetMap.Position - userPos;

                RaisePredictiveEvent(new BlinkEvent(GetNetEntity(weaponUid), direction));
                return;
            }
            // WD EDIT END

            ClientHeavyAttack(userOverride, coordinates, weaponUid, weapon);
            return;
        }

        // Light attack
        if (useDown == BoundKeyState.Down)
        {
            var attackerPos = TransformSystem.GetMapCoordinates(userOverride);

            if (mousePos.MapId != attackerPos.MapId ||
                (attackerPos.Position - mousePos.Position).Length() > weapon.Range)
            {
                return;
            }

            EntityUid? target = null;

            if (_stateManager.CurrentState is GameplayStateBase screen)
            {
                target = screen.GetDamageableClickedEntity(mousePos); // Goob edit
            }

            // Don't light-attack if interaction will be handling this instead // Mono - add hands check (why is this duplicated?)
            if (HasComp<HandsComponent>(userOverride) && Interaction.CombatModeCanHandInteract(userOverride, target))
                return;

            RaisePredictiveEvent(new LightAttackEvent(GetNetEntity(target), GetNetEntity(weaponUid), GetNetCoordinates(coordinates)));
        }

        if (useDown == BoundKeyState.Down)
            ClientLightAttack(userOverride, mousePos, coordinates, weaponUid, weapon);
    }

    protected override bool InRange(EntityUid user, EntityUid target, float range, ICommonSession? session)
    {
        var xform = Transform(target);
        var targetCoordinates = xform.Coordinates;
        var targetLocalAngle = xform.LocalRotation;

        return Interaction.InRangeUnobstructed(user, target, targetCoordinates, targetLocalAngle, range, overlapCheck: false);
    }

    protected override void DoDamageEffect(List<EntityUid> targets, EntityUid? user, TransformComponent targetXform)
    {
        // Server never sends the event to us for predictiveeevent.
        _color.RaiseEffect(Color.Red, targets, Filter.Local());
    }

    protected override bool DoDisarm(EntityUid user, DisarmAttackEvent ev, EntityUid meleeUid, MeleeWeaponComponent component, ICommonSession? session)
    {
        if (!base.DoDisarm(user, ev, meleeUid, component, session))
            return false;

        if (!TryComp<CombatModeComponent>(user, out var combatMode) ||
            combatMode.CanDisarm != true)
        {
            return false;
        }

        var target = GetEntity(ev.Target);

        // They need to either have hands...
        if (!HasComp<HandsComponent>(target!.Value))
        {
            // or just be able to be shoved over.
            if (TryComp<StatusEffectsComponent>(target, out var status) && status.AllowedEffects.Contains("KnockedDown"))
                return true;

            if (Timing.IsFirstTimePredicted && HasComp<MobStateComponent>(target.Value))
                PopupSystem.PopupEntity(Loc.GetString("disarm-action-disarmable", ("targetName", target.Value)), target.Value);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Raises a heavy attack event with the relevant attacked entities.
    /// This is to avoid lag effecting the client's perspective too much.
    /// </summary>
    // Mono - dual wielding
    /// <summary>
    /// While dual-wielding, each hand gets its own mouse button and only ever does the wide swing.
    /// Guns in either hand are left to GunSystem.
    /// </summary>
    private void UpdateDualWield(Entity<DualWieldComponent> entity)
    {
        if (!CombatMode.IsInCombatMode(entity))
        {
            StopDualAttack(entity, true);
            StopDualAttack(entity, false);
            return;
        }

        var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);

        if (mousePos.MapId == MapId.Nullspace)
            return;

        EntityCoordinates coordinates;

        if (MapManager.TryFindGridAt(mousePos, out var gridUid, out _))
            coordinates = TransformSystem.ToCoordinates(gridUid, mousePos);
        else
            coordinates = TransformSystem.ToCoordinates(_map.GetMap(mousePos.MapId), mousePos);

        TryDualAttack(entity, true, coordinates);
        TryDualAttack(entity, false, coordinates);
    }

    private void TryDualAttack(Entity<DualWieldComponent> entity, bool left, EntityCoordinates coordinates)
    {
        if (!TryGetDualMelee(entity, left, out var weaponUid, out var weapon))
            return;

        var key = left ? EngineKeyFunctions.Use : EngineKeyFunctions.UseSecondary;
        var down = _inputSystem.CmdStates.GetState(key) == BoundKeyState.Down;

        if (weapon.AutoAttack || !down)
        {
            if (weapon.Attacking)
                RaisePredictiveEvent(new StopAttackEvent(GetNetEntity(weaponUid)));
        }

        if (!down || weapon.Attacking || weapon.NextAttack > Timing.CurTime)
            return;

        if (!Blocker.CanAttack(entity, weapon: (weaponUid, weapon)))
            return;

        ClientHeavyAttack(entity, coordinates, weaponUid, weapon);
    }

    private void StopDualAttack(Entity<DualWieldComponent> entity, bool left)
    {
        if (TryGetDualMelee(entity, left, out _, out var weapon))
            weapon.Attacking = false;
    }

    private bool TryGetDualMelee(Entity<DualWieldComponent> entity, bool left, out EntityUid weaponUid, [NotNullWhen(true)] out MeleeWeaponComponent? weapon)
    {
        weaponUid = default;
        weapon = null;

        if (!_dualWield.TryGetDualWeapon((entity.Owner, entity.Comp), left, out var held))
            return false;

        // Guns are driven by GunSystem, even ones that also have a melee component.
        if (HasComp<GunComponent>(held))
            return false;

        // The server rejects wide swings from weapons that can't do them, so don't bother asking.
        if (!TryComp(held, out weapon) || weapon.MustBeEquippedToUse || !weapon.CanWideSwing)
            return false;

        weaponUid = held;
        return true;
    }
    // End Mono

    private void ClientHeavyAttack(EntityUid user, EntityCoordinates coordinates, EntityUid meleeUid, MeleeWeaponComponent component)
    {
        // Only run on first prediction to avoid the potential raycast entities changing.
        if (!_xformQuery.TryGetComponent(user, out var userXform) ||
            !Timing.IsFirstTimePredicted)
        {
            return;
        }

        var targetMap = TransformSystem.ToMapCoordinates(coordinates);

        if (targetMap.MapId != userXform.MapID)
            return;

        var userPos = TransformSystem.GetWorldPosition(userXform);
        var direction = targetMap.Position - userPos;
        var distance = MathF.Min(component.Range, direction.Length());

        // This should really be improved. GetEntitiesInArc uses pos instead of bounding boxes.
        // Server will validate it with InRangeUnobstructed.
        var entities = GetNetEntityList(ArcRayCast(userPos, direction.ToWorldAngle(), component.Angle, distance, userXform.MapID, user).ToList());
        RaisePredictiveEvent(new HeavyAttackEvent(GetNetEntity(meleeUid), entities.GetRange(0, Math.Min(MaxTargets, entities.Count)), GetNetCoordinates(coordinates)));
    }

    private void ClientDisarm(EntityUid attacker, MapCoordinates mousePos, EntityCoordinates coordinates)
    {
        EntityUid? target = null;

        if (_stateManager.CurrentState is GameplayStateBase screen)
            target = screen.GetClickedEntity(mousePos);

        RaisePredictiveEvent(new DisarmAttackEvent(GetNetEntity(target), GetNetCoordinates(coordinates)));
    }

    private void ClientLightAttack(EntityUid attacker, MapCoordinates mousePos, EntityCoordinates coordinates, EntityUid weaponUid, MeleeWeaponComponent meleeComponent)
    {
        var attackerPos = TransformSystem.GetMapCoordinates(attacker);

        if (mousePos.MapId != attackerPos.MapId || (attackerPos.Position - mousePos.Position).Length() > meleeComponent.Range)
            return;

        EntityUid? target = null;

        if (_stateManager.CurrentState is GameplayStateBase screen)
            target = screen.GetClickedEntity(mousePos);

        // Don't light-attack if interaction will be handling this instead // Mono - only if attacker has hands
        if (HasComp<HandsComponent>(attacker) && Interaction.CombatModeCanHandInteract(attacker, target))
            return;

        RaisePredictiveEvent(new LightAttackEvent(GetNetEntity(target), GetNetEntity(weaponUid), GetNetCoordinates(coordinates)));
    }

    private void OnMeleeLunge(MeleeLungeEvent ev)
    {
        var ent = GetEntity(ev.Entity);
        var entWeapon = GetEntity(ev.Weapon);

        // Entity might not have been sent by PVS.
        if (Exists(ent) && Exists(entWeapon))
            DoLunge(ent, entWeapon, ev.Angle, ev.LocalPos, ev.Animation);
    }
}
