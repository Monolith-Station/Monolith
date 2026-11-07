using System.Numerics;
using Content.Shared.Conveyor;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Threading;
using Robust.Shared.Network; // Mono
using Robust.Shared.Timing; // Mono
using Robust.Shared.Utility; // Mono

namespace Content.Shared.Physics.Controllers;

public abstract partial class SharedConveyorController : VirtualController
{
    [Dependency] protected IMapManager MapManager = default!;
    [Dependency] private   IParallelManager _parallel = default!;
    [Dependency] private   CollisionWakeSystem _wake = default!;
    [Dependency] protected EntityLookupSystem Lookup = default!;
    [Dependency] private   FixtureSystem _fixtures = default!;
    [Dependency] private   SharedGravitySystem _gravity = default!;
    [Dependency] protected   SharedMoverController _mover = default!; // Mono: private -> protected
    [Dependency] protected IGameTiming Timing = default!; // Mono
    [Dependency] private INetManager _net = default!; // Mono

    protected const string ConveyorFixture = "conveyor";

    private ConveyorJob _job;

    private EntityQuery<ConveyorComponent> _conveyorQuery;
    private EntityQuery<ConveyedComponent> _conveyedQuery;
    protected EntityQuery<PhysicsComponent> PhysicsQuery;
    protected EntityQuery<TransformComponent> XformQuery;

    protected HashSet<EntityUid> Intersecting = new();
    protected Dictionary<Entity<ConveyorComponent>, List<(Entity<ConveyedComponent>, float)>> ConveyedPerBelt = []; // Mono

    public override void Initialize()
    {
        _job = new ConveyorJob(this);
        _conveyorQuery = GetEntityQuery<ConveyorComponent>();
        _conveyedQuery = GetEntityQuery<ConveyedComponent>();
        PhysicsQuery = GetEntityQuery<PhysicsComponent>();
        XformQuery = GetEntityQuery<TransformComponent>();

        UpdatesAfter.Add(typeof(SharedMoverController));

        SubscribeLocalEvent<ConveyedComponent, TileFrictionEvent>(OnConveyedFriction);
        SubscribeLocalEvent<ConveyedComponent, ComponentStartup>(OnConveyedStartup);
        SubscribeLocalEvent<ConveyedComponent, ComponentShutdown>(OnConveyedShutdown);

        SubscribeLocalEvent<ConveyorComponent, StartCollideEvent>(OnConveyorStartCollide);
        SubscribeLocalEvent<ConveyorComponent, ComponentStartup>(OnConveyorStartup);
        SubscribeLocalEvent<ConveyorComponent, ComponentShutdown>(OnConveyorShutdown); // Mono

        base.Initialize();
    }

    private void OnConveyedFriction(Entity<ConveyedComponent> ent, ref TileFrictionEvent args)
    {
        // Conveyed entities don't get friction, they just get wishdir applied so will inherently slowdown anyway.
        args.Modifier = 0f;
    }

    private void OnConveyedStartup(Entity<ConveyedComponent> ent, ref ComponentStartup args)
    {
        // We need waking / sleeping to work and don't want collisionwake interfering with us.
        _wake.SetEnabled(ent.Owner, false);
    }

    private void OnConveyedShutdown(Entity<ConveyedComponent> ent, ref ComponentShutdown args)
    {
        _wake.SetEnabled(ent.Owner, true);
    }

    private void OnConveyorStartup(Entity<ConveyorComponent> ent, ref ComponentStartup args)
    {
        AwakenConveyor(ent.Owner);
    }

    // Mono start
    protected virtual void OnConveyorShutdown(EntityUid uid, ConveyorComponent component, ComponentShutdown args)
    {
        var conveyorEnt = (uid, component);
        if (!ConveyedPerBelt.TryGetValue(conveyorEnt, out var conveyorContents))
            return;

        conveyorContents.Clear();
        ConveyedPerBelt.Remove(conveyorEnt);
    }
    // Mono end

    /// <summary>
    /// Forcefully awakens all entities near the conveyor.
    /// </summary>
    protected virtual void AwakenConveyor(Entity<TransformComponent?> ent)
    {
    }

    protected virtual void PrepAndSortQueue() {} // Mono

    private void OnConveyorStartCollide(Entity<ConveyorComponent> conveyor, ref StartCollideEvent args)
    {
        if (!CanRun(conveyor.Comp)) return; // Mono

        var otherUid = args.OtherEntity;

        if (!args.OtherFixture.Hard || args.OtherBody.BodyType == BodyType.Static)
            return;

        var otherComp = EnsureComp<ConveyedComponent>(otherUid); // Mono
        TryEnqueue(conveyor, (otherUid, otherComp)); // Mono
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        _job.Prediction = prediction;
        _job.Conveyed.Clear();
        PrepAndSortQueue(); // Mono

        var query = EntityQueryEnumerator<ConveyedComponent, FixturesComponent, PhysicsComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var comp, out var fixtures, out var physics, out var xform))
        {
            // Mono start
            // TODO: We really could axe out the EntityQueryEnumerator + query.MoveNext calls here,
            // and instead use our ConveyorPerBelt list instead of querying whatever's the number of currently conveyed entities
            // (that could either be moving or waiting to be moved). Or make ConveyorPerBelt be a PriorityQueue and work from there.
            // But my brain is tired. I feel like at this point it's better to just rework belts to be factorio style.
            // (see https://www.factorio.com/blog/post/fff-176)
            if (_mover.UsedMobMovement.TryGetValue(uid, out var usedMob) && usedMob) // mobs with movement always go in no matter what
            {
                _job.Conveyed.Add(((uid, comp, fixtures, physics, xform), Vector2.Zero, true, false));
                continue;
            }

            var claimedAndMatured = comp.Claimed() && comp.Matured(Timing.CurTime);

            if (!physics.Awake && !claimedAndMatured) // not ready to process yet
                continue;

            if (claimedAndMatured && !physics.Awake) // ready to process but body isn't awake - wake it
                PhysicsSystem.WakeBody(uid);
            // Mono end

            _job.Conveyed.Add(((uid, comp, fixtures, physics, xform), Vector2.Zero, false, false));
        }

        _parallel.ProcessNow(_job, _job.Conveyed.Count);

        foreach (var ent in _job.Conveyed)
        {
            if (!ent.Entity.Comp3.Predict && prediction)
                continue;

            var physics = ent.Entity.Comp3;
            var velocity = physics.LinearVelocity;
            var targetDir = ent.Direction;

            // If mob is moving with the conveyor then combine the directions.
            var wishDir = _mover.GetWishDir(ent.Entity.Owner);

            if (Vector2.Dot(wishDir, targetDir) > 0f)
            {
                targetDir += wishDir;
            }

            if (ent.Result)
            {
                SetConveying(ent.Entity.Owner, ent.Entity.Comp1, targetDir.LengthSquared() > 0f);

                // We apply friction here so when we push items towards the center of the conveyor they don't go overspeed.
                // We also don't want this to apply to mobs as they apply their own friction and otherwise
                // they'll go too slow.
                if (!_mover.UsedMobMovement.TryGetValue(ent.Entity.Owner, out var usedMob) || !usedMob)
                {
                    // We provide a small minimum friction speed as well for those times where the friction would stop large objects
                    // snagged on corners from sliding into the centerline.
                    _mover.Friction(0.2f, frameTime: frameTime, friction: 5f, ref velocity);
                }

                SharedMoverController.Accelerate(ref velocity, targetDir, 20f, frameTime);
            }
            else if (!_mover.UsedMobMovement.TryGetValue(ent.Entity.Owner, out var usedMob) || !usedMob)
            {
                // Need friction to outweigh the movement as it will bounce a bit against the wall.
                // This facilitates being able to sleep entities colliding into walls.
                _mover.Friction(0f, frameTime: frameTime, friction: 40f, ref velocity);
            }

            PhysicsSystem.SetLinearVelocity(ent.Entity.Owner, velocity, wakeBody: false);

            if (!IsConveyed((ent.Entity.Owner, ent.Entity.Comp2)))
            {
                RemComp<ConveyedComponent>(ent.Entity.Owner);
            }
        }
    }

    private void SetConveying(EntityUid uid, ConveyedComponent conveyed, bool value)
    {
        if (conveyed.Conveying == value)
            return;

        conveyed.Conveying = value;
        // Dirty(uid, conveyed); // Mono: Add back when pulling is actually predicted and the networked variable is actually needed
    }

    /// <summary>
    /// Gets the conveying direction for an entity.
    /// </summary>
    /// <returns>False if we should no longer be considered actively conveyed.</returns>
    private bool TryConvey(Entity<ConveyedComponent, FixturesComponent, PhysicsComponent, TransformComponent> entity,
        bool prediction, bool isMob, // Mono: added isMob
        out Vector2 direction)
    {
        direction = Vector2.Zero;
        var fixtures = entity.Comp2;
        var physics = entity.Comp3;
        var xform = entity.Comp4;

        if (!physics.Awake)
            return true;

        // Client moment
        if (!physics.Predict && prediction)
            return true;

        if (xform.GridUid == null)
            return true;

        if (physics.BodyStatus == BodyStatus.InAir ||
            _gravity.IsWeightless(entity.Owner))
        {
            return true;
        }

        Entity<ConveyorComponent> bestConveyor = default;
        var bestSpeed = 0f;
        var contacts = PhysicsSystem.GetContacts((entity.Owner, fixtures));
        var transform = PhysicsSystem.GetPhysicsTransform(entity.Owner);
        var anyConveyors = false;

        while (contacts.MoveNext(out var contact))
        {
            if (!contact.IsTouching)
                continue;

            // Check if our center is over their fixture otherwise ignore it.
            var other = contact.OtherEnt(entity.Owner);

            // Check for blocked, if so then we can't convey at all and just try to sleep
            // Otherwise we may just keep pushing it into the wall

            if (!_conveyorQuery.TryComp(other, out var conveyor))
                continue;

            anyConveyors = true;
            var otherFixture = contact.OtherFixture(entity.Owner);
            var otherTransform = PhysicsSystem.GetPhysicsTransform(other);

            // Check if our center is over the conveyor, otherwise ignore it.
            if (!_fixtures.TestPoint(otherFixture.Item2.Shape, otherTransform, transform.Position))
                continue;

            if (conveyor.Speed > bestSpeed && CanRun(conveyor))
            {
                bestSpeed = conveyor.Speed;
                bestConveyor = (other, conveyor);
            }
        }

        // If we have no touching contacts we shouldn't be using conveyed anyway so nuke it.
        if (!anyConveyors)
            return true;

        if (bestSpeed == 0f || bestConveyor == default)
            return true;

        if (Timing.CurTime < entity.Comp1.MoveReadyAt && !isMob) // Mono
            return false; // Mono

        var comp = bestConveyor.Comp!;
        var conveyorXform = XformQuery.GetComponent(bestConveyor.Owner);
        var (conveyorPos, conveyorRot) = TransformSystem.GetWorldPositionRotation(conveyorXform);

        conveyorRot += bestConveyor.Comp!.Angle;

        if (comp.State == ConveyorState.Reverse)
            conveyorRot += MathF.PI;

        var conveyorDirection = conveyorRot.ToWorldVec();
        direction = conveyorDirection;

        var itemRelative = conveyorPos - transform.Position;
        direction = Convey(direction, bestSpeed, itemRelative);

        // Do a final check for hard contacts so if we're conveying into a wall then NOOP.
        contacts = PhysicsSystem.GetContacts((entity.Owner, fixtures));

        while (contacts.MoveNext(out var contact))
        {
            if (!contact.Hard || !contact.IsTouching)
                continue;

            var other = contact.OtherEnt(entity.Owner);
            var otherBody = contact.OtherBody(entity.Owner);

            // If the blocking body is dynamic then don't ignore it for this.
            if (otherBody.BodyType != BodyType.Static)
                continue;

            var otherTransform = PhysicsSystem.GetPhysicsTransform(other);
            var dotProduct = Vector2.Dot(otherTransform.Position - transform.Position, direction);

            // TODO: This should probably be based on conveyor speed, this is mainly so we don't
            // go to sleep when conveying and colliding with tables perpendicular to the conveyance direction.
            if (dotProduct > 1.5f)
            {
                direction = Vector2.Zero;
                return false;
            }
        }

        return true;
    }
    private static Vector2 Convey(Vector2 direction, float speed, Vector2 itemRelative)
    {
        if (speed == 0 || direction.LengthSquared() == 0)
            return Vector2.Zero;

        /*
         * Basic idea: if the item is not in the middle of the conveyor in the direction that the conveyor is running,
         * move the item towards the middle. Otherwise, move the item along the direction. This lets conveyors pick up
         * items that are not perfectly aligned in the middle, and also makes corner cuts work.
         *
         * We do this by computing the projection of 'itemRelative' on 'direction', yielding a vector 'p' in the direction
         * of 'direction'. We also compute the rejection 'r'. If the magnitude of 'r' is not (near) zero, then the item
         * is not on the centerline.
         */

        var p = direction * (Vector2.Dot(itemRelative, direction) / Vector2.Dot(direction, direction));
        var r = itemRelative - p;

        // 0.01 is considered close enough to the centerline that (most) large objects shouldn't
        // snag on walls next to the conveyor, without smaller entities repeatedly overshooting.
        if (r.Length() < 0.01)
        {
            var velocity = direction * speed;
            return velocity;
        }
        else
        {
            // Give a slight nudge in the direction of the conveyor to prevent
            // to collidable objects (e.g. crates) on the locker from getting stuck
            // pushing each other when rounding a corner.
            // The direction of the conveyorbelt is de-emphasized to ensure offset objects primarily push centerwards,
            // to prevent large items getting snagged on corner turns.
            // 0.2f seems like a good compromise between forwards and sideways movement.
            var velocity = (r + direction * 0.2f).Normalized() * speed;
            return velocity;
        }
    }

    public bool CanRun(ConveyorComponent component)
    {
        return component.State != ConveyorState.Off && component.Powered;
    }

    private record struct ConveyorJob : IParallelRobustJob
    {
        public int BatchSize => 16;

        public List<(Entity<ConveyedComponent, FixturesComponent, PhysicsComponent, TransformComponent> Entity, Vector2 Direction, bool IsMob, bool Result)> Conveyed = new(); // Mono: added IsMob

        public SharedConveyorController System;

        public bool Prediction;

        public ConveyorJob(SharedConveyorController controller)
        {
            System = controller;
        }

        public void Execute(int index)
        {
            var convey = Conveyed[index];

            var result = System.TryConvey(
                (convey.Entity.Owner, convey.Entity.Comp1, convey.Entity.Comp2, convey.Entity.Comp3, convey.Entity.Comp4),
                Prediction, convey.IsMob, out var direction); // Mono: added IsMob

            Conveyed[index] = (convey.Entity, direction, convey.IsMob, result); // Mono: added IsMob
        }
    }

    /// <summary>
    /// Checks an entity's contacts to see if it's still being conveyed.
    /// </summary>
    private bool IsConveyed(Entity<FixturesComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return false;

        var contacts = PhysicsSystem.GetContacts(ent.Owner);

        while (contacts.MoveNext(out var contact))
        {
            if (!contact.IsTouching)
                continue;

            var other = contact.OtherEnt(ent.Owner);

            if (_conveyorQuery.TryComp(other, out var comp) && CanRun(comp))
                return true;
        }

        return false;
    }

    // Mono start
    /// <summary>
    /// Tries to enqueue a passed conveyed entity into the passed conveyor's batch system
    /// </summary>
    /// <param name="conveyor"></param>
    /// <param name="conveyed"></param>
    /// <returns></returns>
    protected bool TryEnqueue(Entity<ConveyorComponent> conveyor, Entity<ConveyedComponent> conveyed)
    {
        if (!_net.IsServer) // no need to run this on client
            return false;

        if (conveyor == conveyed.Comp.CurrentConveyor) // Skip entries already added for this conveyor
            return false;

        // Gets conveyor rotation, alongside angle and accounting for reverse...
        var conveyorRot = TransformSystem.GetWorldRotation(conveyor.Owner);
        conveyorRot += conveyor.Comp.Angle;
        if (conveyor.Comp.State == ConveyorState.Reverse)
            conveyorRot += MathF.PI;

        var conveyorDirection = conveyorRot.ToWorldVec(); // And transform it into a direction.
        ConveyedPerBelt.GetOrNew(conveyor).Add(( // Adds conveyor entry if it doesnt exist, along with the conveyed entry for it, and...
            (conveyed.Owner, conveyed.Comp!),
            Vector2.Dot(TransformSystem.GetWorldPosition(conveyed.Owner), conveyorDirection) // ...scalar projection for sorting.
        ));

        return true;
    }
    // Mono end
}
