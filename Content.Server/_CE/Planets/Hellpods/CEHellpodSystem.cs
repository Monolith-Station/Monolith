using Content.Shared._CE.Planets.Hellpods;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.DragDrop;
using Content.Shared.DoAfter;
using Content.Shared.Alert;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._CE.Planets.Hellpods;

public sealed partial class CEHellpodSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private CESharedZLevelsSystem _levels = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private AlertsSystem _alerts = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEHellpodHatchComponent, DragDropTargetEvent>(OnBoard);
        SubscribeLocalEvent<CEHellpodHatchComponent, CEHellpodBoardDoAfterEvent>(OnBoardComplete);
        SubscribeLocalEvent<BodyComponent, CEHellpodExitAlertEvent>(OnExit);
        SubscribeLocalEvent<CEHellpodHatchComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<CEHellpodComponent, ContainerIsRemovingAttemptEvent>(OnRemoveAttempt);
        SubscribeLocalEvent<CEHellpodComponent, CEZLevelHitEvent>(OnLand);
    }

    private ContainerSlot Seat(EntityUid uid) => _containers.EnsureContainer<ContainerSlot>(uid, "occupant");

    private void OnBoard(Entity<CEHellpodHatchComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled || args.User != args.Dragged || !HasComp<BodyComponent>(args.User)
            || Seat(ent).ContainedEntity != null)
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.EntryDelay,
            new CEHellpodBoardDoAfterEvent(), ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        });
    }

    private void OnBoardComplete(Entity<CEHellpodHatchComponent> ent, ref CEHellpodBoardDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || Seat(ent).ContainedEntity != null
            || !HasComp<BodyComponent>(args.User) || !_containers.Insert(args.User, Seat(ent)))
            return;
        args.Handled = true;
        _alerts.ShowAlert(args.User, "CEHellpodHatch");
        _appearance.SetData(ent, CEHellpodVisuals.Occupied, true);
        ent.Comp.LaunchAt = null;
    }

    private void OnExit(Entity<BodyComponent> ent, ref CEHellpodExitAlertEvent args)
    {
        var hatch = Transform(ent).ParentUid;
        if (args.Handled || !HasComp<CEHellpodHatchComponent>(hatch) || Seat(hatch).ContainedEntity != ent.Owner)
            return;
        args.Handled = _containers.Remove(ent.Owner, Seat(hatch));
    }

    private void OnRemoved(Entity<CEHellpodHatchComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != "occupant")
            return;
        _alerts.ClearAlert(args.Entity, "CEHellpodHatch");
        ent.Comp.LaunchAt = null;
        _appearance.SetData(ent, CEHellpodVisuals.Occupied, false);
    }

    private void OnRemoveAttempt(Entity<CEHellpodComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (ent.Comp.InFlight && args.Container.ID == "occupant")
            args.Cancel();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<CEHellpodHatchComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var hatch, out var xform))
        {
            if (Seat(uid).ContainedEntity is not { } occupant || !HasComp<CEZMapComponent>(xform.MapUid))
            {
                hatch.LaunchAt = null;
                continue;
            }

            hatch.LaunchAt ??= _timing.CurTime + hatch.LaunchDelay;
            if (_timing.CurTime < hatch.LaunchAt.Value)
                continue;

            var pod = Spawn(hatch.Pod, xform.Coordinates);
            if (!_containers.Insert(occupant, Seat(pod)))
            {
                QueueDel(pod);
                hatch.LaunchAt = null;
                continue;
            }

            Comp<CEHellpodComponent>(pod).InFlight = true;
            _levels.TryMoveDown(pod);
            var physics = Comp<CEZPhysicsComponent>(pod);
            physics.LocalPosition = 0.9f;
            physics.Velocity = -5f;
            Dirty(pod, physics);
            _levels.WakeBody((pod, physics));
        }
    }

    private void OnLand(Entity<CEHellpodComponent> ent, ref CEZLevelHitEvent args)
    {
        if (!ent.Comp.InFlight)
            return;
        ent.Comp.InFlight = false;
        if (Seat(ent).ContainedEntity is { } occupant)
            _containers.Remove(occupant, Seat(ent));
    }
}
