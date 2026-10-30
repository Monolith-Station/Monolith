using Content.Server.DeviceLinking.Systems;
using Content.Server.Materials;
using Content.Shared.Conveyor;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Destructible;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Physics.Controllers;
using Content.Shared.Power;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility; // Mono

namespace Content.Server.Physics.Controllers;

public sealed partial class ConveyorController : SharedConveyorController
{
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private DeviceLinkSystem _signalSystem = default!;
    [Dependency] private MaterialReclaimerSystem _materialReclaimer = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(MoverController));
        SubscribeLocalEvent<ConveyorComponent, ComponentInit>(OnInit);
        // SubscribeLocalEvent<ConveyorComponent, ComponentShutdown>(OnConveyorShutdown); // Mono: Moved to SharedConveyorController
        SubscribeLocalEvent<ConveyorComponent, BreakageEventArgs>(OnBreakage);

        SubscribeLocalEvent<ConveyorComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<ConveyorComponent, PowerChangedEvent>(OnPowerChanged);

        base.Initialize();
    }

    private void OnInit(EntityUid uid, ConveyorComponent component, ComponentInit args)
    {
        _signalSystem.EnsureSinkPorts(uid, component.ReversePort, component.ForwardPort, component.OffPort);

        if (PhysicsQuery.TryComp(uid, out var physics))
        {
            var shape = new PolygonShape();
            shape.SetAsBox(0.55f, 0.55f);

            _fixtures.TryCreateFixture(uid, shape, ConveyorFixture,
                collisionLayer: (int) (CollisionGroup.LowImpassable | CollisionGroup.MidImpassable |
                                       CollisionGroup.Impassable), hard: false, body: physics);

        }
    }

    protected override void OnConveyorShutdown(EntityUid uid, ConveyorComponent component, ComponentShutdown args) // Mono: private void -> protected override void
    {
        base.OnConveyorShutdown(uid, component, args); // Mono
        if (MetaData(uid).EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        if (!PhysicsQuery.TryComp(uid, out var physics))
            return;

        _fixtures.DestroyFixture(uid, ConveyorFixture, body: physics);
    }

    // Mono start
    protected override void PrepAndSortQueue()
    {
        foreach (var belt in ConveyedPerBelt)
        {
            // sort descending by scalar projection onto belt direction, to get who's in front; can be removed if that's unnecessary
            if (belt.Value.Count > belt.Key.Comp.BatchSize) // sorts only if we're above batch size to avoid unnecessary calls; if we're below or equal, all items will be moved anyway (hopefully)
                belt.Value.Sort((a, b) => b.Item2.CompareTo(a.Item2));

            foreach (var conveyed in belt.Value)
            {
                conveyed.Item1.Comp.SetCurrentBelt(belt.Key, Timing.CurTime); // Sets current belt for the conveyed comp;
            }

            belt.Value.Clear(); // clears the list values, not the conveyors entries on the dict, as to mantain the list size so GC don't eat them for no reason. TODO: review if we need a system to handle removing conveyor entries that go stale (more than a minute or a configurable amount of time without receiving any conveyed entity)
        }
    }
    // Mono end;

    private void OnBreakage(Entity<ConveyorComponent> ent, ref BreakageEventArgs args)
    {
        SetState(ent, ConveyorState.Off, ent);
    }

    private void OnPowerChanged(EntityUid uid, ConveyorComponent component, ref PowerChangedEvent args)
    {
        component.Powered = args.Powered;
        component.Reset(); // Mono
        UpdateAppearance(uid, component);
        Dirty(uid, component);
    }

    private void UpdateAppearance(EntityUid uid, ConveyorComponent component)
    {
        _appearance.SetData(uid, ConveyorVisuals.State, component.Powered ? component.State : ConveyorState.Off);
    }

    private void OnSignalReceived(EntityUid uid, ConveyorComponent component, ref SignalReceivedEvent args)
    {
        if (args.Port == component.OffPort)
            SetState(uid, ConveyorState.Off, component);

        else if (args.Port == component.ForwardPort)
        {
            SetState(uid, ConveyorState.Forward, component);
        }

        else if (args.Port == component.ReversePort)
        {
            SetState(uid, ConveyorState.Reverse, component);
        }
    }

    private void SetState(EntityUid uid, ConveyorState state, ConveyorComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!_materialReclaimer.SetReclaimerEnabled(uid, state != ConveyorState.Off))
            return;

        component.State = state;
        component.Reset(); // Mono

        if (state != ConveyorState.Off)
        {
            EnsureConveyed(uid, component); // Mono: Renamed WakeConveyed to EnsureConveyed, added component arg
        }

        UpdateAppearance(uid, component);
        Dirty(uid, component);
    }

    /// <summary>
    /// Awakens sleeping entities on the conveyor belt's tile when it's turned on.
    /// Need this as we might activate under CollisionWake entities and need to forcefully check them.
    /// </summary>
    protected override void AwakenConveyor(Entity<TransformComponent?> ent)
    {
        if (!XformQuery.Resolve(ent.Owner, ref ent.Comp))
            return;

        var xform = ent.Comp;

        var beltTileRef = _turf.GetTileRef(xform.Coordinates);

        if (beltTileRef != null)
        {
            Intersecting.Clear();
            Lookup.GetLocalEntitiesIntersecting(beltTileRef.Value.GridUid, beltTileRef.Value.GridIndices, Intersecting, 0f, flags: LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Approximate);

            foreach (var entity in Intersecting)
            {
                if (!PhysicsQuery.TryGetComponent(entity, out var physics))
                    continue;

                if (physics.BodyType != BodyType.Static)
                    PhysicsSystem.WakeBody(entity, body: physics);
            }
        }
    }

    // Mono start
    /// <summary>
    /// Ensures conveyed component to all entities touching contacting this conveyor.
    /// </summary>
    private void EnsureConveyed(EntityUid conveyorUid, ConveyorComponent? conveyorComp)
    {
        var contacts = PhysicsSystem.GetContacts(conveyorUid);
        var conveyor = (conveyorUid, conveyorComp!);

        while (contacts.MoveNext(out var contact))
        {
            var other = contact.OtherEnt(conveyorUid);
            ConveyedComponent? otherComp = default;

            if (contact.OtherFixture(conveyorUid).Item2.Hard && contact.OtherBody(conveyorUid).BodyType != BodyType.Static)
                otherComp = EnsureComp<ConveyedComponent>(other);

            if (Resolve(other, ref otherComp))
            {
                TryEnqueue(conveyor, (other, otherComp));
            }
        }

        ConveyedPerBelt.GetOrNew(conveyor).Sort((a, b) => b.Item2.CompareTo(a.Item2));
    }
    // Mono end
}
