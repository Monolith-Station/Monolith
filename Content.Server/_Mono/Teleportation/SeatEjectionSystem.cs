using Content.Shared.Actions;
using Content.Shared.Buckle.Components;
using Content.Server.Teleportation;
using Content.Shared._Mono.Teleportation;

namespace Content.Server.EjectionSeat;

public sealed partial class EjectionSeatSystem : EntitySystem
{

    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private TeleportSystem _teleportSys = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SeatEjectionComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<SeatEjectionComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<SeatEjectionComponent, SeatEjectionEjectEvent>(OnEject);
    }

    private void OnStrapped(Entity<SeatEjectionComponent> seat, ref StrappedEvent args)
    {
        _actionsSystem.AddAction(args.Buckle, ref seat.Comp.ActionUid, seat.Comp.ActionProto, seat);
    }

    private void OnUnstrapped(Entity<SeatEjectionComponent> seat, ref UnstrappedEvent args)
    {
        _actionsSystem.RemoveAction(args.Buckle, seat.Comp.ActionUid);
        seat.Comp.ActionUid = null;
    }

    private void OnEject(Entity<SeatEjectionComponent> seat, ref SeatEjectionEjectEvent args)
    {
            _actionsSystem.RemoveAction(args.Performer, seat.Comp.ActionUid);
            _teleportSys.RandomTeleport(seat, seat.Comp.Specifier); //Teleport the WHOLE seat with you cause its funny
            RemComp<SeatEjectionComponent>(seat);
    }
}
