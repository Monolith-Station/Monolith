using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.Systems
{
    public abstract partial class SharedShuttleConsoleSystem : EntitySystem
    {
        [Dependency] protected ActionBlockerSystem ActionBlockerSystem = default!;
        [Dependency] private SharedMoverController _mover = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<PilotComponent, UpdateCanMoveEvent>(HandleMovementBlock);
            SubscribeLocalEvent<PilotComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<PilotComponent, ComponentShutdown>(HandlePilotShutdown);
        }

        [Serializable, NetSerializable]
        protected sealed class PilotComponentState(NetEntity? uid) : ComponentState
        {
            public NetEntity? Console { get; } = uid;
        }

        protected virtual void HandlePilotShutdown(EntityUid uid, PilotComponent component, ComponentShutdown args)
        {
            ActionBlockerSystem.UpdateCanMove(uid);

            if (TryComp<InputMoverComponent>(uid, out var inputMover))
            {
                inputMover.CanMove = true;
                Dirty(uid, inputMover);
            }

            if (!TryComp<PausedPilotingRelayComponent>(uid, out var pausedRelay))
                return;

            if (pausedRelay.RelayTarget.IsValid() && Exists(pausedRelay.RelayTarget))
                _mover.SetRelay(uid, pausedRelay.RelayTarget);
            else
                RemComp<RelayInputMoverComponent>(uid);

            RemComp<PausedPilotingRelayComponent>(uid);
        }

        private void OnStartup(EntityUid uid, PilotComponent component, ComponentStartup args)
        {
            ActionBlockerSystem.UpdateCanMove(uid);

            if (TryComp<InputMoverComponent>(uid, out var inputMover))
            {
                inputMover.CanMove = false;
                Dirty(uid, inputMover);
            }

            if (!TryComp<RelayInputMoverComponent>(uid, out var relayCompToPause))
                return;

            var pausedRelay = EnsureComp<PausedPilotingRelayComponent>(uid);
            pausedRelay.RelayTarget = relayCompToPause.RelayEntity;
            Dirty(uid, pausedRelay);

            RemComp<RelayInputMoverComponent>(uid);
        }

        private void HandleMovementBlock(EntityUid uid, PilotComponent component, UpdateCanMoveEvent args)
        {
            if (component.LifeStage > ComponentLifeStage.Running)
                return;
            if (component.Console == null)
                return;

            args.Cancel();
        }
    }
}
