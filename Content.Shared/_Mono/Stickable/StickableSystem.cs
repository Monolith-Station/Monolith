using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Shared._Mono.Stickable;

public sealed partial class StickableSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<PhysicsComponent> _bodyQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StickableComponent, AfterInteractEvent>(OnInteract);
        SubscribeLocalEvent<StickableComponent, EntParentChangedMessage>(OnParentChange);

        _bodyQuery = GetEntityQuery<PhysicsComponent>();
    }

    private void OnInteract(Entity<StickableComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !_timing.IsFirstTimePredicted)
            return;

        if (args.Target is not { } target)
            return;

        var newCoord = _transform.WithEntityId(args.ClickLocation, target);

        _audio.PlayPvs(ent.Comp.AttachSound, ent);
        if (ent.Comp.ReuseProto is not { } spawnProto)
        {
            AttachTo(ent, newCoord);
        }
        else if (_net.IsServer)
        {
            var newUid = Spawn(spawnProto);
            if (TryComp<StickableComponent>(newUid, out var newStick))
                AttachTo((newUid, newStick), newCoord);
        }

        args.Handled = true;
    }

    private void OnParentChange(Entity<StickableComponent> ent, ref EntParentChangedMessage args)
    {
        if (args.Transform.ParentUid == ent.Comp.AttachedParent || !_bodyQuery.TryComp(ent, out var body))
            return;

        ent.Comp.AttachedParent = null;
        _physics.SetBodyType(ent, BodyType.Dynamic, body: body);
    }

    private void AttachTo(Entity<StickableComponent> ent, EntityCoordinates to)
    {
        if (!_bodyQuery.TryComp(ent, out var body))
            return;

        ent.Comp.AttachedParent = to.EntityId;

        _transform.SetCoordinates(ent, to);
        _physics.SetLinearVelocity(ent, Vector2.Zero, body: body);
        _physics.SetBodyType(ent, BodyType.Static, body: body);
    }
}
