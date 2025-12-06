using Content.Server.Cargo.Systems;
using Content.Server.Power.Components;
using Content.Shared._Mono.CCVar;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Mono.Cleanup;

/// <summary>
/// This system cleans up small grid fragments that have less than a specified number of tiles after a delay.
/// </summary>
public sealed class GridCleanupSystem : BaseCleanupSystem<MapGridComponent>
{
    [Dependency] private readonly CleanupHelperSystem _cleanup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;

    private float _maxDistance;
    private float _maxValue;
    private TimeSpan _duration;

    private HashSet<Entity<ApcComponent>> _apcList = new();

    private EntityQuery<BatteryComponent> _batteryQuery;
    private EntityQuery<CleanupImmuneComponent> _immuneQuery;

    public override void Initialize()
    {
        base.Initialize();

        _batteryQuery = GetEntityQuery<BatteryComponent>();
        _immuneQuery = GetEntityQuery<CleanupImmuneComponent>();

        Subs.CVar(_cfg, MonoCVars.GridCleanupDistance, val => _maxDistance = val, true);
        Subs.CVar(_cfg, MonoCVars.GridCleanupMaxValue, val => _maxValue = val, true);
        Subs.CVar(_cfg, MonoCVars.GridCleanupDuration, val => _duration = TimeSpan.FromSeconds(val), true);
    }

    protected override bool ShouldEntityCleanup(EntityUid uid)
    {
        var xform = Transform(uid);
        var parent = xform.ParentUid;

        var state = EnsureComp<GridCleanupStateComponent>(uid);

        if (HasComp<MapComponent>(uid) // if we're a planetmap ignore
            || !HasComp<MapGridComponent>(uid) // if we somehow lost MapGridComponent
            || HasComp<MapGridComponent>(parent) // do not delete anything on planetmaps either
            || _immuneQuery.HasComp(uid)
            || TryComp<IFFComponent>(uid, out var iff) && (iff.Flags & IFFFlags.HideLabel) == 0 // delete only if IFF off
            || _cleanup.HasNearbyPlayers(xform.Coordinates, _maxDistance)
            || HasPoweredAPC((uid, xform)) // don't delete if it has powered APCs
            || _pricing.AppraiseGrid(uid) > _maxValue) // expensive to run, put last
        {
            state.CleanupAccumulator = TimeSpan.FromSeconds(0);
            return false;
        }
        // see if we should update timer or just be deleted
        else if (state.CleanupAccumulator < _duration)
        {
            state.CleanupAccumulator += _cleanupInterval;
            return false;
        }

        return true;
    }

    bool HasPoweredAPC(Entity<TransformComponent> grid)
    {
        _apcList.Clear();
        var worldAABB = _lookup.GetWorldAABB(grid, grid.Comp);

        _lookup.GetEntitiesIntersecting<ApcComponent>(grid.Comp.MapID, worldAABB, _apcList);

        foreach (var apc in _apcList)
        {
            // charge check should ideally be a comparision to 0f but i don't trust that
            if (_batteryQuery.TryComp(apc, out var battery)
                && battery.CurrentCharge > battery.MaxCharge * 0.01f
                && apc.Comp.MainBreakerEnabled // if it's disabled consider it depowered
            )
                return true;
        }
        return false;
    }
}
