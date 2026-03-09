using Content.Server.Cargo.Systems;
using Content.Server.NPC.HTN;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono.CCVar;
using Content.Shared.Mind.Components;
using Content.Shared.Physics;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using System.Reflection;

namespace Content.Server._Mono.Cleanup;

/// <summary>
///     Deletes entities eligible for deletion.
/// </summary>
public sealed class SpaceCleanupSystem : BaseCleanupSystem<PhysicsComponent>
{
    [Dependency] private readonly CleanupHelperSystem _cleanup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private object _manifold = default!;
    private MethodInfo _testOverlap = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private float _maxDistance;
    private float _maxGridDistance;
    private float _maxPrice;

    private EntityQuery<CleanupImmuneComponent> _immuneQuery;
    private EntityQuery<FixturesComponent> _fixQuery;
    private EntityQuery<HTNComponent> _htnQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<MindContainerComponent> _mindQuery;
    private EntityQuery<PhysicsComponent> _physQuery;

    private List<(EntityCoordinates Coord, TimeSpan Time, float Radius, float Aggression)> _sweepQueue = new();
    private HashSet<Entity<PhysicsComponent>> _sweepEnts = new();

    public override void Initialize()
    {
        base.Initialize();

        // this queries over literally everything with PhysicsComponent so has to have big interval
        _cleanupInterval = TimeSpan.FromSeconds(600);

        _immuneQuery = GetEntityQuery<CleanupImmuneComponent>();
        _fixQuery = GetEntityQuery<FixturesComponent>();
        _htnQuery = GetEntityQuery<HTNComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _mindQuery = GetEntityQuery<MindContainerComponent>();
        _physQuery = GetEntityQuery<PhysicsComponent>();

        Subs.CVar(_cfg, MonoCVars.CleanupMaxGridDistance, val => _maxGridDistance = val, true);
        Subs.CVar(_cfg, MonoCVars.SpaceCleanupDistance, val => _maxDistance = val, true);
        Subs.CVar(_cfg, MonoCVars.SpaceCleanupMaxValue, val => _maxPrice = val, true);

        var manifoldType = typeof(SharedMapSystem).Assembly.GetType("Robust.Shared.Physics.Collision.IManifoldManager");
        if (manifoldType != null)
        {
            _manifold = IoCManager.ResolveType(manifoldType);
            var testOverlapMethod = manifoldType.GetMethod("TestOverlap");
            if (testOverlapMethod != null)
                _testOverlap = testOverlapMethod.MakeGenericMethod(typeof(IPhysShape), typeof(PhysShapeCircle));
        }
    }

    protected override bool ShouldEntityCleanup(EntityUid uid)
    {
        return ShouldEntityCleanup(uid, 1f);
    }

    private bool ShouldEntityCleanup(EntityUid uid, float aggression)
    {
        var xform = Transform(uid);

        var isStuck = false;

        var price = 0f;

        return !_gridQuery.HasComp(uid)
            && (xform.ParentUid == xform.MapUid // don't delete if on grid
                || (isStuck |= GetWallStuck((uid, xform)))) // or wall-stuck
            && !_htnQuery.HasComp(uid) // handled by MobCleanupSystem
            && !_immuneQuery.HasComp(uid) // handled by GridCleanupSystem
            && !_mindQuery.HasComp(uid) // no deleting anything that can have a mind - should be handled by MobCleanupSystem anyway
            && (price = (float)_pricing.GetPrice(uid)) <= _maxPrice
            && (isStuck
                || !_cleanup.HasNearbyGrids(xform.Coordinates, _maxGridDistance * aggression * MathF.Sqrt(price / _maxPrice))
                    && !_cleanup.HasNearbyPlayers(xform.Coordinates, _maxDistance * aggression * MathF.Sqrt(price / _maxPrice)));
    }

    private bool GetWallStuck(Entity<TransformComponent> ent)
    {
        if (ent.Comp.GridUid is not { } gridUid
            || ent.Comp.Anchored
            || ent.Comp.ParentUid != gridUid // ignore if not directly parented to grid
        )
            return false;

        var xfB = new Transform(ent.Comp.LocalPosition, 0);
        var shapeB = new PhysShapeCircle(0.001f);

        var contacts = _physics.GetContacts(ent.Owner);
        // it dies without this for some reason
        if (contacts == ContactEnumerator.Empty)
            return false;

        while (contacts.MoveNext(out var contact))
        {
            if (contact.FixtureA == null
                || contact.FixtureB == null
                || contact.BodyA == null
                || contact.BodyB == null
                || !contact.FixtureA.Hard
                || !contact.FixtureB.Hard
                || !contact.IsTouching
            )
                continue;

            var isA = contact.EntityB == ent.Owner;

            var body = isA ? contact.BodyA : contact.BodyB;
            // only trigger when the other entity is static
            if ((body.BodyType & BodyType.Static) == 0)
                continue;

            var fix = isA ? contact.FixtureA : contact.FixtureB;
            var xform = isA ? contact.XformA : contact.XformB;
            var anch = isA ? contact.EntityA : contact.EntityB;

            var xf = _physics.GetLocalPhysicsTransform(anch, xform);
            var shape = fix.Shape;

            if ((bool?)_testOverlap.Invoke(_manifold, [shape, 0, shapeB, 0, xf, xfB]) ?? false)
                return true;
        }

        return false;
    }

    public void QueueSweep(EntityCoordinates coordinates, TimeSpan time, float radius, float aggression)
    {
        _sweepQueue.Add((coordinates, time, radius, aggression));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        for (int i = _sweepQueue.Count - 1; i >= 0; i--)
        {
            var (coord, time, radius, aggression) = _sweepQueue[i];

            if (_timing.CurTime < time)
                continue;

            _sweepQueue.RemoveAt(i);
            if (!coord.IsValid(EntityManager))
                continue;

            _sweepEnts.Clear();
            _lookup.GetEntitiesInRange(_transform.ToMapCoordinates(coord), radius, _sweepEnts, LookupFlags.Dynamic | LookupFlags.Approximate | LookupFlags.Sundries);

            foreach (var (uid, body) in _sweepEnts)
            {
                if (ShouldEntityCleanup(uid, aggression))
                    CleanupEnt(uid);
            }
        }
    }
}
