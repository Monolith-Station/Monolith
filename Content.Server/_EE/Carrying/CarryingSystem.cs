using System.Numerics;
using System.Threading;
using Content.Server.DoAfter;
using Content.Server.Resist;
using Content.Server.Popups;
using Content.Server.Inventory;
using Content.Server.Nyanotrasen.Item.PseudoItem;
using Content.Shared.Body.Systems; // Forge-Change
using Content.Shared.Mobs;
using Content.Shared.DoAfter;
using Content.Shared.Buckle.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands;
using Content.Shared.Stunnable;
using Content.Shared.Interaction.Events;
using Content.Shared.Verbs;
using Content.Shared.Climbing.Events;
using Content.Shared.Carrying;
using Content.Shared.Contests;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.ActionBlocker;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Throwing;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Implants.Components; // Forge-Change
using Content.Shared.Mobs.Systems;
using Content.Shared.Nyanotrasen.Item.PseudoItem;
using Content.Shared.Storage;
using Content.Shared.Traits.Assorted; //Mono: Wheelchair user check
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Server.GameObjects;

namespace Content.Server.Carrying
{
    public sealed partial class CarryingSystem : EntitySystem
    {
        [Dependency] private readonly VirtualItemSystem _virtualItemSystem = default!;
        [Dependency] private readonly CarryingSlowdownSystem _slowdown = default!;
        [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly StandingStateSystem _standingState = default!;
        [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
        [Dependency] private readonly PullingSystem _pullingSystem = default!;
        [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
        [Dependency] private readonly EscapeInventorySystem _escapeInventorySystem = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
        [Dependency] private readonly PseudoItemSystem _pseudoItem = default!;
        [Dependency] private readonly ContestsSystem _contests = default!;
        [Dependency] private readonly TransformSystem _transform = default!;
        [Dependency] private readonly SharedBodySystem _body = default!; // Forge-Change

        public const float BaseDistanceCoeff = 0.5f; // Frontier: default throwing speed reduction
        public const float MaxDistanceCoeff = 1.0f; // Frontier: default throwing speed reduction
        public const float DefaultMaxThrowDistance = 4.0f; // Frontier: maximum throwing distance

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<CarriableComponent, GetVerbsEvent<AlternativeVerb>>(AddCarryVerb);
            SubscribeLocalEvent<CarryingComponent, GetVerbsEvent<InnateVerb>>(AddInsertCarriedVerb);
            SubscribeLocalEvent<CarryingComponent, VirtualItemDeletedEvent>(OnVirtualItemDeleted);
            SubscribeLocalEvent<CarryingComponent, BeforeThrowEvent>(OnThrow);
            SubscribeLocalEvent<CarryingComponent, EntParentChangedMessage>(OnParentChanged);
            SubscribeLocalEvent<CarryingComponent, MobStateChangedEvent>(OnMobStateChanged);
            SubscribeLocalEvent<BeingCarriedComponent, InteractionAttemptEvent>(OnInteractionAttempt);
            SubscribeLocalEvent<BeingCarriedComponent, MoveInputEvent>(OnMoveInput);
            SubscribeLocalEvent<BeingCarriedComponent, UpdateCanMoveEvent>(OnMoveAttempt);
            SubscribeLocalEvent<BeingCarriedComponent, StandAttemptEvent>(OnStandAttempt);
            SubscribeLocalEvent<BeingCarriedComponent, GettingInteractedWithAttemptEvent>(OnInteractedWith);
            SubscribeLocalEvent<BeingCarriedComponent, PullAttemptEvent>(OnPullAttempt);
            SubscribeLocalEvent<BeingCarriedComponent, StartClimbEvent>(OnStartClimb);
            SubscribeLocalEvent<BeingCarriedComponent, BuckledEvent>(OnBuckleChange);
            SubscribeLocalEvent<BeingCarriedComponent, UnbuckledEvent>(OnBuckleChange);
            SubscribeLocalEvent<BeingCarriedComponent, StrappedEvent>(OnBuckleChange);
            SubscribeLocalEvent<BeingCarriedComponent, UnstrappedEvent>(OnBuckleChange);
            SubscribeLocalEvent<CarriableComponent, CarryDoAfterEvent>(OnDoAfter);
        }

        private void AddCarryVerb(EntityUid uid, CarriableComponent component, GetVerbsEvent<AlternativeVerb> args)
        {
            if (!args.CanInteract || !args.CanAccess || !_mobStateSystem.IsAlive(args.User)
                || !CanCarry(args.User, uid, component)
                || HasComp<CarryingComponent>(args.User)
                || HasComp<BeingCarriedComponent>(args.User) || HasComp<BeingCarriedComponent>(args.Target)
                || args.User == args.Target)
                return;

            AlternativeVerb verb = new()
            {
                Act = () =>
                {
                    StartCarryDoAfter(args.User, uid, component);
                },
                Text = Loc.GetString("carry-verb"),
                Priority = 2
            };
            args.Verbs.Add(verb);
        }

        private void AddInsertCarriedVerb(EntityUid uid, CarryingComponent component, GetVerbsEvent<InnateVerb> args)
        {
            // If the person is carrying someone, and the carried person is a pseudo-item, and the target entity is a storage,
            // then add an action to insert the carried entity into the target
            var toInsert = args.Using;
            if (toInsert is not { Valid: true } || !args.CanAccess
                || !TryComp<PseudoItemComponent>(toInsert, out var pseudoItem)
                || !TryComp<StorageComponent>(args.Target, out var storageComp)
                || !_pseudoItem.CheckItemFits((toInsert.Value, pseudoItem), (args.Target, storageComp)))
                return;

            InnateVerb verb = new()
            {
                Act = () =>
                {
                    DropCarried(uid, toInsert.Value);
                    _pseudoItem.TryInsert(args.Target, toInsert.Value, pseudoItem, storageComp);
                },
                Text = Loc.GetString("action-name-insert-other", ("target", toInsert)),
                Priority = 2
            };
            args.Verbs.Add(verb);
        }

        /// <summary>
        /// Since the carried entity is stored as 2 virtual items, when deleted we want to drop them.
        /// </summary>
        private void OnVirtualItemDeleted(EntityUid uid, CarryingComponent component, VirtualItemDeletedEvent args)
        {
            if (!HasComp<CarriableComponent>(args.BlockingEntity))
                return;

            DropCarried(uid, args.BlockingEntity);
        }

        /// <summary>
        /// Basically using virtual item passthrough to throw the carried person. A new age!
        /// Maybe other things besides throwing should use virt items like this...
        /// </summary>
        private void OnThrow(EntityUid uid, CarryingComponent component, ref BeforeThrowEvent args)
        {
            if (!TryComp<VirtualItemComponent>(args.ItemUid, out var virtItem)
                || !HasComp<CarriableComponent>(virtItem.BlockingEntity))
                return;

            args.ItemUid = virtItem.BlockingEntity;

            // Forge-Change-Start: R.I.P.L.Y carry assist affects throwing carried entities.
            var contestCoeff = GetCarryMassContest(uid, virtItem.BlockingEntity, false, 2f)
                                * _contests.StaminaContest(uid, virtItem.BlockingEntity);
            // Forge-Change-End

            // Frontier: sanitize our range regardless of CVar values - TODO: variable throw distance ranges (via traits, etc.)
            contestCoeff = float.Min(BaseDistanceCoeff * contestCoeff, MaxDistanceCoeff);
            if (args.Direction.Length() > DefaultMaxThrowDistance * contestCoeff)
                args.Direction = args.Direction.Normalized() * DefaultMaxThrowDistance * contestCoeff;
            // End Frontier
        }

        private void OnParentChanged(EntityUid uid, CarryingComponent component, ref EntParentChangedMessage args)
        {
            var xform = Transform(uid);
            if (xform.MapUid != args.OldMapId || xform.ParentUid == xform.GridUid)
                return;

            DropCarried(uid, component.Carried);
        }

        private void OnMobStateChanged(EntityUid uid, CarryingComponent component, MobStateChangedEvent args)
        {
            DropCarried(uid, component.Carried);
        }

        /// <summary>
        /// Only let the person being carried interact with their carrier and things on their person.
        /// </summary>
        private void OnInteractionAttempt(EntityUid uid, BeingCarriedComponent component, InteractionAttemptEvent args)
        {
            if (args.Target == null)
                return;

            var targetParent = Transform(args.Target.Value).ParentUid;

            if (args.Target.Value != component.Carrier && targetParent != component.Carrier && targetParent != uid)
                args.Cancelled = true;
        }

        /// <summary>
        /// Try to escape via the escape inventory system.
        /// </summary>
        private void OnMoveInput(EntityUid uid, BeingCarriedComponent component, ref MoveInputEvent args)
        {
            if (!TryComp<CanEscapeInventoryComponent>(uid, out var escape)
                || !args.HasDirectionalMovement)
                return;

            // Check if the victim is in any way incapacitated, and if not make an escape attempt.
            // Escape time scales with the inverse of a mass contest. Being lighter makes escape harder.
            if (_actionBlockerSystem.CanInteract(uid, component.Carrier))
            {
                var disadvantage = _contests.MassContest(component.Carrier, uid, false, 2f);
                _escapeInventorySystem.AttemptEscape(uid, component.Carrier, escape, disadvantage);
            }
        }

        private void OnMoveAttempt(EntityUid uid, BeingCarriedComponent component, UpdateCanMoveEvent args)
        {
            args.Cancel();
        }

        private void OnStandAttempt(EntityUid uid, BeingCarriedComponent component, StandAttemptEvent args)
        {
            args.Cancel();
        }

        private void OnInteractedWith(EntityUid uid, BeingCarriedComponent component, GettingInteractedWithAttemptEvent args)
        {
            if (args.Uid != component.Carrier)
                args.Cancelled = true;
        }

        private void OnPullAttempt(EntityUid uid, BeingCarriedComponent component, PullAttemptEvent args)
        {
            args.Cancelled = true;
        }

        private void OnStartClimb(EntityUid uid, BeingCarriedComponent component, ref StartClimbEvent args)
        {
            DropCarried(component.Carrier, uid);
        }

        private void OnBuckleChange<TEvent>(EntityUid uid, BeingCarriedComponent component, TEvent args)
        {
            DropCarried(component.Carrier, uid);
        }

        private void OnDoAfter(EntityUid uid, CarriableComponent component, CarryDoAfterEvent args)
        {
            component.CancelToken = null;
            if (args.Handled || args.Cancelled
                || !CanCarry(args.Args.User, uid, component))
                return;

            Carry(args.Args.User, uid);
            args.Handled = true;
        }
        private void StartCarryDoAfter(EntityUid carrier, EntityUid carried, CarriableComponent component)
        {
            // Forge-Change-Start: R.I.P.L.Y raises the hard carry mass limit.
            if (!TryComp<PhysicsComponent>(carrier, out var carrierPhysics)
                || !TryComp<PhysicsComponent>(carried, out var carriedPhysics)
                || carriedPhysics.Mass > GetCarryMassLimit(carrier, carrierPhysics))
            {
                _popupSystem.PopupEntity(Loc.GetString("carry-too-heavy"), carried, carrier, Shared.Popups.PopupType.SmallCaution);
                return;
            }
            // Forge-Change-End

            var length = component.PickupDuration // Frontier: removed outer TimeSpan.FromSeconds()
                        // Forge-Change: R.I.P.L.Y makes the carrier count as heavier during pickup timing.
                        * _contests.MassContest(carriedPhysics, false, 4f, GetEffectiveCarryMass(carrier, carrierPhysics))
                        * _contests.StaminaContest(carrier, carried)
                        * (_standingState.IsDown(carried) ? 0.5f : 1);

            // Frontier: sanitize pickup time duration regardless of CVars - no near-instant pickups.
            var durationSeconds = float.Clamp(length, component.MinPickupDuration, component.MaxPickupDuration);
            // Forge-Change: Apply R.I.P.L.Y pickup delay bonus after the max-duration clamp.
            durationSeconds = MathF.Max(component.MinPickupDuration, durationSeconds * GetCarryPickupDelayModifier(carrier));
            var duration = TimeSpan.FromSeconds(durationSeconds);
            // End Frontier

            component.CancelToken = new CancellationTokenSource();

            var ev = new CarryDoAfterEvent();
            var args = new DoAfterArgs(EntityManager, carrier, duration, ev, carried, target: carried) // Frontier: length<duration
            {
                BreakOnMove = true,
                NeedHand = true,
                MultiplyDelay = false, // Goobstation
            };

            _doAfterSystem.TryStartDoAfter(args);

            // Show a popup to the person getting picked up
            _popupSystem.PopupEntity(Loc.GetString("carry-started", ("carrier", carrier)), carried, carried);
        }

        private void Carry(EntityUid carrier, EntityUid carried)
        {
            if (TryComp<PullableComponent>(carried, out var pullable))
                _pullingSystem.TryStopPull(carried, pullable);

            _transform.AttachToGridOrMap(carrier);
            _transform.AttachToGridOrMap(carried);
            _transform.SetCoordinates(carried, Transform(carrier).Coordinates);
            _transform.SetParent(carried, carrier);

            _virtualItemSystem.TrySpawnVirtualItemInHand(carried, carrier);
            _virtualItemSystem.TrySpawnVirtualItemInHand(carried, carrier);
            var carryingComp = EnsureComp<CarryingComponent>(carrier);
            ApplyCarrySlowdown(carrier, carried);
            var carriedComp = EnsureComp<BeingCarriedComponent>(carried);
            EnsureComp<KnockedDownComponent>(carried);

            carryingComp.Carried = carried;
            carriedComp.Carrier = carrier;

            _actionBlockerSystem.UpdateCanMove(carried);
        }

        public bool TryCarry(EntityUid carrier, EntityUid toCarry, CarriableComponent? carriedComp = null)
        {
            // Forge-Change-Start: R.I.P.L.Y raises the public TryCarry mass limit too.
            if (!Resolve(toCarry, ref carriedComp, false)
                || !CanCarry(carrier, toCarry, carriedComp)
                || HasComp<BeingCarriedComponent>(carrier)
                || HasComp<ItemComponent>(carrier)
                || TryComp<PhysicsComponent>(carrier, out var carrierPhysics)
                && TryComp<PhysicsComponent>(toCarry, out var toCarryPhysics)
                && toCarryPhysics.Mass > GetCarryMassLimit(carrier, carrierPhysics))
                return false;
            // Forge-Change-End

            Carry(carrier, toCarry);

            return true;
        }

        public void DropCarried(EntityUid carrier, EntityUid carried)
        {
            RemComp<CarryingComponent>(carrier); // get rid of this first so we don't recursively fire that event
            // Forge-Change-Start: Prevent stale carry slowdown after dropping carried entities.
            if (TryComp<CarryingSlowdownComponent>(carrier, out var slowdown))
                _slowdown.SetModifier(carrier, 1f, 1f, slowdown);
            // Forge-Change-End
            RemComp<CarryingSlowdownComponent>(carrier);
            RemComp<BeingCarriedComponent>(carried);
            RemComp<KnockedDownComponent>(carried);
            _actionBlockerSystem.UpdateCanMove(carried);
            _virtualItemSystem.DeleteInHandsMatching(carrier, carried);
            _transform.AttachToGridOrMap(carried);
            if (!HasComp<LegsParalyzedComponent>(carried)) // Mono: Check wheelchair user before standing
                _standingState.Stand(carried);
            _movementSpeed.RefreshMovementSpeedModifiers(carrier);
        }

        private void ApplyCarrySlowdown(EntityUid carrier, EntityUid carried)
        {
            // Forge-Change: R.I.P.L.Y affects carrying slowdown mass contest.
            var massRatio = GetCarryMassContest(carrier, carried, true);
            var massRatioSq = MathF.Pow(massRatio, 2);
            var modifier = 1 - 0.15f / massRatioSq;
            modifier = Math.Max(0.1f, modifier);
            modifier = ApplyCarryAssistSlowdown(carrier, modifier); // Forge-Change

            var slowdownComp = EnsureComp<CarryingSlowdownComponent>(carrier);
            _slowdown.SetModifier(carrier, modifier, modifier, slowdownComp);
        }

        // Forge-Change-Start: R.I.P.L.Y carry assist helpers.
        // Preserve the original 2x hard carry limit, but raise it for R.I.P.L.Y.
        private float GetCarryMassLimit(EntityUid carrier, PhysicsComponent carrierPhysics)
        {
            return carrierPhysics.Mass * Math.Max(2f, GetCarryAssistMassMultiplier(carrier));
        }

        // Cybernetic R.I.P.L.Y nails make the carrier count as heavier only for carry contests.
        private float GetEffectiveCarryMass(EntityUid carrier, PhysicsComponent carrierPhysics)
        {
            return carrierPhysics.Mass * GetCarryAssistMassMultiplier(carrier);
        }

        // Mass contest variant that respects the R.I.P.L.Y effective carry mass.
        private float GetCarryMassContest(EntityUid carrier, EntityUid carried, bool bypassClamp = false, float rangeFactor = 1f)
        {
            if (GetCarryAssistMassMultiplier(carrier) <= 1f)
                return _contests.MassContest(carrier, carried, bypassClamp, rangeFactor);

            if (!TryComp<PhysicsComponent>(carrier, out var carrierPhysics) ||
                !TryComp<PhysicsComponent>(carried, out var carriedPhysics) ||
                carriedPhysics.Mass == 0)
            {
                return 1f;
            }

            return GetEffectiveCarryMass(carrier, carrierPhysics) / carriedPhysics.Mass;
        }

        // Cybernetic R.I.P.L.Y nails make the pickup do-after a little easier.
        private float GetCarryPickupDelayModifier(EntityUid carrier)
        {
            var modifier = 1f;
            var query = EntityQueryEnumerator<SubdermalImplantComponent>();
            while (query.MoveNext(out _, out var implant))
            {
                if (implant.ImplantedEntity == carrier)
                    modifier = Math.Min(modifier, implant.CarryAssistPickupDelayModifier);
            }

            // Forge-Change-Start: R.I.P.L.Y cybernetic hands only grant carry assist as an installed pair.
            if (HasPairedCarryAssistParts(carrier))
            {
                foreach (var (partUid, part) in _body.GetBodyChildren(carrier))
                {
                    if (!part.Enabled || !TryComp<SubdermalImplantComponent>(partUid, out var assist))
                        continue;

                    modifier = Math.Min(modifier, assist.CarryAssistPickupDelayModifier);
                }
            }
            // Forge-Change-End

            return modifier;
        }

        // Reduce only the slowdown penalty, leaving the base carrying system intact.
        private float ApplyCarryAssistSlowdown(EntityUid carrier, float modifier)
        {
            var penaltyModifier = GetCarryAssistSlowdownPenaltyModifier(carrier);
            if (penaltyModifier >= 1f)
                return modifier;

            return 1f - (1f - modifier) * penaltyModifier;
        }

        private float GetCarryAssistMassMultiplier(EntityUid carrier)
        {
            var multiplier = 1f;
            var query = EntityQueryEnumerator<SubdermalImplantComponent>();
            while (query.MoveNext(out _, out var implant))
            {
                if (implant.ImplantedEntity == carrier)
                    multiplier = Math.Max(multiplier, implant.CarryAssistMassMultiplier);
            }

            // Forge-Change-Start: R.I.P.L.Y cybernetic hands only grant carry assist as an installed pair.
            if (HasPairedCarryAssistParts(carrier))
            {
                foreach (var (partUid, part) in _body.GetBodyChildren(carrier))
                {
                    if (!part.Enabled || !TryComp<SubdermalImplantComponent>(partUid, out var assist))
                        continue;

                    multiplier = Math.Max(multiplier, assist.CarryAssistMassMultiplier);
                }
            }
            // Forge-Change-End

            return multiplier;
        }

        private float GetCarryAssistSlowdownPenaltyModifier(EntityUid carrier)
        {
            var modifier = 1f;
            var query = EntityQueryEnumerator<SubdermalImplantComponent>();
            while (query.MoveNext(out _, out var implant))
            {
                if (implant.ImplantedEntity == carrier)
                    modifier = Math.Min(modifier, implant.CarryAssistSlowdownPenaltyModifier);
            }

            // Forge-Change-Start: R.I.P.L.Y cybernetic hands only grant carry assist as an installed pair.
            if (HasPairedCarryAssistParts(carrier))
            {
                foreach (var (partUid, part) in _body.GetBodyChildren(carrier))
                {
                    if (!part.Enabled || !TryComp<SubdermalImplantComponent>(partUid, out var assist))
                        continue;

                    modifier = Math.Min(modifier, assist.CarryAssistSlowdownPenaltyModifier);
                }
            }
            // Forge-Change-End

            return modifier;
        }

        // Forge-Change-Start: Paired body-part support for R.I.P.L.Y carry assist.
        private bool HasPairedCarryAssistParts(EntityUid carrier)
        {
            var count = 0;
            foreach (var (partUid, part) in _body.GetBodyChildren(carrier))
            {
                if (!part.Enabled ||
                    !TryComp<SubdermalImplantComponent>(partUid, out var assist) ||
                    !HasCarryAssist(assist))
                {
                    continue;
                }

                count++;
                if (count >= 2)
                    return true;
            }

            return false;
        }

        private static bool HasCarryAssist(SubdermalImplantComponent assist)
        {
            return assist.CarryAssistMassMultiplier > 1f ||
                   assist.CarryAssistPickupDelayModifier < 1f ||
                   assist.CarryAssistSlowdownPenaltyModifier < 1f;
        }
        // Forge-Change-End
        // Forge-Change-End

        public bool CanCarry(EntityUid carrier, EntityUid carried, CarriableComponent? carriedComp = null)
        {
            if (!Resolve(carried, ref carriedComp, false)
                || carriedComp.CancelToken != null
                || !HasComp<MapGridComponent>(Transform(carrier).ParentUid)
                || HasComp<BeingCarriedComponent>(carrier)
                || HasComp<BeingCarriedComponent>(carried)
                || !TryComp<HandsComponent>(carrier, out var hands)
                || hands.CountFreeHands() < carriedComp.FreeHandsRequired)
                return false;

            return true;
        }

        public override void Update(float frameTime)
        {
            var query = EntityQueryEnumerator<BeingCarriedComponent>();
            while (query.MoveNext(out var carried, out var comp))
            {
                var carrier = comp.Carrier;
                if (carrier is not { Valid: true } || carried is not { Valid: true })
                    continue;

                // SOMETIMES - when an entity is inserted into disposals, or a cryosleep chamber - it can get re-parented without a proper reparent event
                // when this happens, it needs to be dropped because it leads to weird behavior
                if (Transform(carried).ParentUid != carrier)
                {
                    DropCarried(carrier, carried);
                    continue;
                }

                // Make sure the carried entity is always centered relative to the carrier, as gravity pulls can offset it otherwise
                var xform = Transform(carried);
                if (!xform.LocalPosition.Equals(Vector2.Zero))
                {
                    xform.LocalPosition = Vector2.Zero;
                }
            }
            query.Dispose();
        }
    }
}
