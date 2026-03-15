using System.Numerics;
using Content.Shared.CombatMode;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Movement.Events;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

// Mono
using Robust.Shared.Player;
using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Misc;

public abstract class SharedGrapplingGunSystem : VirtualController
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPvsOverrideSystem _pvsOverride = default!; // Mono
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public const string GrapplingJoint = "grappling";

    public override void Initialize()
    {
        SubscribeLocalEvent<GrapplingProjectileComponent, ProjectileEmbedEvent>(OnGrappleCollide);
        SubscribeLocalEvent<GrapplingProjectileComponent, JointRemovedEvent>(OnGrappleJointRemoved);
        SubscribeLocalEvent<CanWeightlessMoveEvent>(OnWeightlessMove);
        SubscribeAllEvent<RequestGrapplingReelMessage>(OnGrapplingReel);

        SubscribeLocalEvent<GrapplingGunComponent, GunShotEvent>(OnGrapplingShot);
        SubscribeLocalEvent<GrapplingGunComponent, ActivateInWorldEvent>(OnGunActivate);
        SubscribeLocalEvent<GrapplingGunComponent, HandDeselectedEvent>(OnGrapplingDeselected);

        UpdatesBefore.Add(typeof(SharedJointSystem)); // We want to run before joints are solved
        base.Initialize();
    }

    private void OnGrappleJointRemoved(EntityUid uid, GrapplingProjectileComponent component, JointRemovedEvent args)
    {
        if (_netManager.IsServer)
            QueueDel(uid);
    }

    private void OnGrapplingShot(EntityUid uid, GrapplingGunComponent component, ref GunShotEvent args)
    {
        foreach (var (shotUid, _) in args.Ammo)
        {
            if (shotUid is not { } shot || !HasComp<GrapplingProjectileComponent>(shotUid))
                continue;

            //todo: this doesn't actually support multigrapple
            // At least show the visuals.
            component.Projectile = shot;
            DirtyField(uid, component, nameof(GrapplingGunComponent.Projectile));
            var visuals = EnsureComp<JointVisualsComponent>(shot);
            visuals.Sprite = component.RopeSprite;
            visuals.OffsetA = new Vector2(0f, 0.5f);
            visuals.Target = GetNetEntity(uid);
            Dirty(shot, visuals);

            // Mono
            if (_player.TryGetSessionByEntity(args.User, out var session))
                _pvsOverride.AddSessionOverride(shot, session);
        }

        TryComp<AppearanceComponent>(uid, out var appearance);
        _appearance.SetData(uid, SharedTetherGunSystem.TetherVisualsStatus.Key, false, appearance);
    }

    private void OnGrapplingDeselected(EntityUid uid, GrapplingGunComponent component, HandDeselectedEvent args)
    {
        SetReeling(uid, component, false, args.User);
    }

    private void OnGrapplingReel(RequestGrapplingReelMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (!TryComp<HandsComponent>(player, out var hands) ||
            !TryComp<GrapplingGunComponent>(hands.ActiveHandEntity, out var grappling))
        {
            return;
        }

        if (msg.Reeling &&
            (!TryComp<CombatModeComponent>(player, out var combatMode) ||
             !combatMode.IsInCombatMode))
        {
            return;
        }

        SetReeling(hands.ActiveHandEntity.Value, grappling, msg.Reeling, player.Value);
    }

    private void OnWeightlessMove(ref CanWeightlessMoveEvent ev)
    {
        if (ev.CanMove || !TryComp<JointRelayTargetComponent>(ev.Uid, out var relayComp))
            return;

        foreach (var relay in relayComp.Relayed)
        {
            if (TryComp<JointComponent>(relay, out var jointRelay) && jointRelay.GetJoints.ContainsKey(GrapplingJoint))
            {
                ev.CanMove = true;
                return;
            }
        }
    }

    /// <summary>
    /// Ungrapples the grappling hook, destroying the hook and severing the joint
    /// </summary>
    /// <param name="grapple">Entity for the grappling gun</param>
    /// <param name="isBreak">Whether to play the sound for the rope breaking</param>
    /// <param name="user">The user responsible for the ungrapple. Optional</param>
    public void Ungrapple(Entity<GrapplingGunComponent> grapple, bool isBreak, EntityUid? user = null)
    {
        if (!Timing.IsFirstTimePredicted || grapple.Comp.Projectile is not { } projectile)
            return;

        if (isBreak)
            _audio.PlayPredicted(grapple.Comp.BreakSound, grapple.Owner, user);

        _appearance.SetData(grapple.Owner, SharedTetherGunSystem.TetherVisualsStatus.Key, true);

        if (_netManager.IsServer)
            QueueDel(projectile);

        SetReeling(grapple.Owner, grapple.Comp, false, user);
        grapple.Comp.Projectile = null;
        DirtyField(grapple.Owner, grapple.Comp, nameof(GrapplingGunComponent.Projectile));
        _gun.ChangeBasicEntityAmmoCount(grapple.Owner, 1);
    }

    private void OnGunActivate(EntityUid uid, GrapplingGunComponent component, ActivateInWorldEvent args)
    {
        if (!Timing.IsFirstTimePredicted || args.Handled || !args.Complex)
            return;

        _audio.PlayPredicted(component.CycleSound, uid, args.User);
        Ungrapple((uid, component), false, args.User);

        args.Handled = true;
    }

    private void SetReeling(EntityUid uid, GrapplingGunComponent component, bool value, EntityUid? user)
    {
        if (TryComp<JointComponent>(uid, out var jointComp) &&
            jointComp.GetJoints.TryGetValue(GrapplingJoint, out var joint) &&
            joint is DistanceJoint distance)
        {
            if (distance.MaxLength <= distance.MinLength + component.RopeFullyReeledMargin)
                value = false;
        }

        if (component.Reeling == value)
            return;

        if (value)
        {
            // We null-coalesce here because playing the sound again will cause it to become eternally stuck playing
            component.Stream ??= _audio.PlayPredicted(component.ReelSound, uid, user)?.Entity ?? component.Stream;
        }
        else if (!value && component.Stream.HasValue && Timing.IsFirstTimePredicted)
        {
            component.Stream = _audio.Stop(component.Stream);
        }

        component.Reeling = value;

        DirtyField(uid, component, nameof(GrapplingGunComponent.Reeling));
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        var query = EntityQueryEnumerator<GrapplingGunComponent, JointComponent>();

        while (query.MoveNext(out var uid, out var grappling, out var jointComp))
        {
            if (!jointComp.GetJoints.TryGetValue(GrapplingJoint, out var joint) ||
                joint is not DistanceJoint distance ||
                !TryComp<JointComponent>(joint.BodyAUid, out var hookJointComp))
            {
                if (_netManager.IsServer) // Client might not receive the joint due to PVS culling, so lets not spam them with 23895739 mispredicted ungrapples
                    Ungrapple((uid, grappling), true);
                continue;
            }

            // If the joint breaks, it gets disabled
            if (distance.Enabled == false)
            {
                Ungrapple((uid, grappling), true);
                continue;
            }

            var physicalGrapple = jointComp.Relay ?? joint.BodyBUid;
            var physicalHook = hookJointComp.Relay ?? joint.BodyAUid;

            // HACK: preventing both ends of the grappling hook from sleeping if neither are on the same grid, so that grid movement works as expected
            if (_transform.GetGrid(physicalHook) != _transform.GetGrid(physicalGrapple))
            {
                _physics.WakeBody(physicalHook);
                _physics.WakeBody(physicalGrapple);
            }
            // END OF HACK

            var bodyAWorldPos = _transform.GetWorldPosition(physicalHook);
            var bodyBWorldPos = _transform.GetWorldPosition(physicalGrapple);

            // The solver does not handle setting the rope's length, but we still need to work with a copy of it to prevent jank.
            var ropeLength = (bodyAWorldPos - bodyBWorldPos).Length();

            // Rope should just break, instantly, if the user is teleported past its max length
            if (ropeLength >= distance.MaxLength + grappling.RopeMargin)
            {
                Ungrapple((uid, grappling), true);
                continue;
            }

            if (!grappling.Reeling)
            {
                // Just in case.
                if (grappling.Stream.HasValue && Timing.IsFirstTimePredicted)
                    grappling.Stream = _audio.Stop(grappling.Stream);

                continue;
            }


            // TODO: Contracting DistanceJoints should be in engine
            if (distance.MaxLength >= ropeLength + grappling.RopeMargin)
            {
                distance.MaxLength = MathF.Max(distance.MinLength + grappling.RopeMargin, distance.MaxLength - grappling.ReelRate * frameTime);
                distance.MaxLength = MathF.Max(ropeLength + grappling.RopeMargin, distance.MaxLength);
                ropeLength = MathF.Min(distance.MaxLength, ropeLength);

                distance.Length = ropeLength;
            }

            if (ropeLength <= distance.MinLength + grappling.RopeFullyReeledMargin)
            {
                SetReeling(uid, grappling, false, null);
            }
            else if (ropeLength >= distance.MaxLength - grappling.RopeMargin)
            {
                var targetDirection = (bodyAWorldPos - bodyBWorldPos).Normalized();

                var grapplerUidA = _container.TryGetOuterContainer(physicalHook, Transform(physicalHook), out var containerA) ? containerA.Owner : physicalHook;
                var grapplerBodyA = Comp<PhysicsComponent>(grapplerUidA);

                _physics.ApplyLinearImpulse(grapplerUidA, targetDirection * grappling.ReelForce * frameTime * -1, body: grapplerBodyA);

                var grapplerUidB = _container.TryGetOuterContainer(physicalGrapple, Transform(physicalGrapple), out var containerB) ? containerB.Owner : physicalGrapple;
                var grapplerBodyB = Comp<PhysicsComponent>(grapplerUidB);

                _physics.ApplyLinearImpulse(grapplerUidB, targetDirection * grappling.ReelForce * frameTime, body: grapplerBodyB);
            }

            Dirty(uid, jointComp);
        }
    }

    private void OnGrappleCollide(EntityUid uid, GrapplingProjectileComponent component, ref ProjectileEmbedEvent args)
    {
        if (!Timing.IsFirstTimePredicted || !TryComp<GrapplingGunComponent>(args.Weapon, out var grapple))
            return;


        var grapplePos = _transform.GetWorldPosition(args.Weapon);
        var hookPos = _transform.GetWorldPosition(uid);
        if ((grapplePos - hookPos).Length() >= grapple.RopeMaxLength)
        {
            Ungrapple((args.Weapon, grapple), true);
            return;
        }

        var joint = _joints.CreateDistanceJoint(uid, args.Weapon, id: GrapplingJoint);
        joint.MaxLength = joint.Length + grapple.RopeMargin;
        joint.Stiffness = grapple.RopeStiffness;
        joint.MinLength = grapple.RopeMinLength; // Length of a tile to prevent pulling yourself into / through walls
        joint.Breakpoint = grapple.RopeBreakPoint;

        var jointCompHook = Comp<JointComponent>(uid); // we use get here because if the component doesn't exist then something has fucked up bigtime
        var jointCompGrapple = Comp<JointComponent>(args.Weapon);

        _joints.SetRelay(uid, args.Embedded, jointCompHook);
        _joints.RefreshRelay(args.Weapon, jointCompGrapple);
    }

    [Serializable, NetSerializable]
    protected sealed class RequestGrapplingReelMessage : EntityEventArgs
    {
        public bool Reeling;

        public RequestGrapplingReelMessage(bool reeling)
        {
            Reeling = reeling;
        }
    }
}
