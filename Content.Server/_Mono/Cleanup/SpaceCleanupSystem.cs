using Content.Server.Cargo.Systems;
using Content.Server.NPC.HTN;
using Content.Shared._Mono.CCVar;
using Content.Shared.Mind.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server._Mono.Cleanup;

/// <summary>
///     Deletes low-value entities floating in space.
/// </summary>
public sealed class SpaceCleanupSystem : BaseCleanupSystem<PhysicsComponent>
{
    [Dependency] private readonly CleanupHelperSystem _cleanup = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;

    private float _maxDistance;
    private float _maxGridDistance;
    private float _maxPrice;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<HTNComponent> _htnQuery;
    private EntityQuery<CleanupImmuneComponent> _immuneQuery;
    private EntityQuery<MindContainerComponent> _mindQuery;

    public override void Initialize()
    {
        base.Initialize();

        // this queries over literally everything with PhysicsComponent so has to have big interval
        _cleanupInterval = TimeSpan.FromSeconds(600);

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _htnQuery = GetEntityQuery<HTNComponent>();
        _immuneQuery = GetEntityQuery<CleanupImmuneComponent>();
        _mindQuery = GetEntityQuery<MindContainerComponent>();

        Subs.CVar(_cfg, MonoCVars.CleanupMaxGridDistance, val => _maxGridDistance = val, true);
        Subs.CVar(_cfg, MonoCVars.SpaceCleanupDistance, val => _maxDistance = val, true);
        Subs.CVar(_cfg, MonoCVars.SpaceCleanupMaxValue, val => _maxPrice = val, true);
    }

    protected override bool ShouldEntityCleanup(EntityUid uid)
    {
        var xform = Transform(uid);

        return xform.ParentUid == xform.MapUid // no deletey if we're on a grid or inside something
            && !_immuneQuery.HasComp(uid)
            && !_htnQuery.HasComp(uid) // handled by MobCleanupSystem
            && !_gridQuery.HasComp(uid) // handled by GridCleanupSystem
            && !_mindQuery.HasComp(uid) // no deleting anything that can have a mind - should be handled by MobCleanupSystem anyway
            && _pricing.GetPrice(uid) <= _maxPrice
            && !_cleanup.HasNearbyPlayers(xform.Coordinates, _maxDistance)
            && !_cleanup.HasNearbyGrids(xform.Coordinates, _maxGridDistance);
    }
}
