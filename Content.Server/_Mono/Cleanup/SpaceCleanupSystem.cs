using Content.Server.Cargo.Systems;
using Content.Server.NPC.HTN;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono.CCVar;
using Content.Shared.Mind.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;
using System.Reflection;

namespace Content.Server._Mono.Cleanup;

/// <summary>
///     Deletes entities eligible for deletion.
/// </summary>
public sealed class SpaceCleanupSystem : BaseCleanupSystem<PhysicsComponent>
{
    [Dependency] private readonly CleanupHelperSystem _cleanup = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;
    private object _manifold = default!;
    private MethodInfo _testOverlap = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;

    private float _maxDistance;
    private float _maxGridDistance;
    private float _maxPrice;

    private EntityQuery<CleanupImmuneComponent> _immuneQuery;
    private EntityQuery<FixturesComponent> _fixQuery;
    private EntityQuery<HTNComponent> _htnQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<MindContainerComponent> _mindQuery;
    private EntityQuery<PhysicsComponent> _physQuery;
    private EntityQuery<SpaceGarbageComponent> _garbageQuery;

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
        _garbageQuery = GetEntityQuery<SpaceGarbageComponent>();

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
        var xform = Transform(uid);

        var isStuck = false;

        return !_gridQuery.HasComp(uid)
            && (xform.ParentUid == xform.MapUid // don't delete if on grid
                || (isStuck |= GetWallStuck((uid, xform)))) // or wall-stuck
            && !_htnQuery.HasComp(uid) // handled by MobCleanupSystem
            && !_immuneQuery.HasComp(uid) // handled by GridCleanupSystem
            && !_mindQuery.HasComp(uid) // no deleting anything that can have a mind - should be handled by MobCleanupSystem anyway
            && _pricing.GetPrice(uid) <= _maxPrice
            && (isStuck
                || !_cleanup.HasNearbyGrids(xform.Coordinates, _maxGridDistance)
                    && !_cleanup.HasNearbyPlayers(xform.Coordinates, _maxDistance));
    }

    private bool GetWallStuck(Entity<TransformComponent> ent)
    {
        if (ent.Comp.GridUid is not { } gridUid
            || ent.Comp.Anchored
            || ent.Comp.ParentUid != gridUid // ignore if not directly parented to grid
            || !_gridQuery.TryComp(gridUid, out var grid)
            || !_physQuery.TryComp(ent, out var body)
        )
            return false;

        var query = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, ent.Comp.Coordinates.ToVector2i(EntityManager, _mapMan, _transform));
        while (query.MoveNext(out var anch))
        {
            if (anch == ent)
                continue;

            if (!_fixQuery.TryComp(anch, out var anchFix))
                continue;

            var xfA = _physics.GetLocalPhysicsTransform(anch.Value);
            var xfB = new Transform(ent.Comp.LocalPosition, 0);
            var i = 0;
            foreach (var (_, fix) in anchFix.Fixtures)
            {
                if (!fix.Hard)
                    continue;

                if ((fix.CollisionLayer & body.CollisionMask) == 0 && (fix.CollisionMask & body.CollisionLayer) == 0)
                    continue;

                var shapeA = fix.Shape;
                var shapeB = new PhysShapeCircle(0.001f);
                if ((bool?)_testOverlap.Invoke(_manifold, [shapeA, i, shapeB, 0, xfA, xfB]) ?? false)
                    return true;
                i++;
            }
        }

        return false;
    }
}
