using Content.Server.Cargo.Systems;
using Content.Server.Power.Components;
using Content.Shared._Mono.CCVar;
using Content.Shared.Power.Components;
using Content.Shared.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Cleanup;

/// <summary>
/// This system cleans up small grid fragments that have less than a specified number of tiles after a delay.
/// </summary>
public sealed partial class GridCleanupSystem : BaseCleanupSystem<MapGridComponent>
{
    [Dependency] private CleanupHelperSystem _cleanup = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private PricingSystem _pricing = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IGameTiming _timing = default!;

    private float _maxDistance;
    private float _maxValue;
    private int _aggressiveTiles;
    private TimeSpan _duration;

    private int _fragmentMaxTiles;
    private TimeSpan _fragmentDuration;
    private float _fragmentMaxValue;
    private float _fragmentDistance;
    private bool _iffProtects; // Forge-Change: IFF no longer blocks cleanup by default

    private HashSet<Entity<ApcComponent>> _apcList = new();

    private EntityQuery<BatteryComponent> _batteryQuery;
    private EntityQuery<CleanupImmuneComponent> _immuneQuery;
    private EntityQuery<MapComponent> _mapCompQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _batteryQuery = GetEntityQuery<BatteryComponent>();
        _immuneQuery = GetEntityQuery<CleanupImmuneComponent>();
        _mapCompQuery = GetEntityQuery<MapComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        Subs.CVar(_cfg, MonoCVars.GridCleanupDistance, val => _maxDistance = val, true);
        Subs.CVar(_cfg, MonoCVars.GridCleanupMaxValue, val => _maxValue = val, true);
        Subs.CVar(_cfg, MonoCVars.GridCleanupDuration, val => _duration = TimeSpan.FromSeconds(val), true);
        Subs.CVar(_cfg, MonoCVars.GridCleanupAggressiveTiles, val => _aggressiveTiles = val, true);
        Subs.CVar(_cfg, MonoCVars.GridCleanupFragmentMaxTiles, val => _fragmentMaxTiles = val, true);
        Subs.CVar(_cfg, MonoCVars.GridCleanupFragmentDuration, val => _fragmentDuration = TimeSpan.FromSeconds(val), true);
        Subs.CVar(_cfg, MonoCVars.GridCleanupFragmentMaxValue, val => _fragmentMaxValue = val, true);
        Subs.CVar(_cfg, MonoCVars.GridCleanupFragmentDistance, val => _fragmentDistance = val, true);
        Subs.CVar(_cfg, MonoCVars.GridCleanupIffProtects, val => _iffProtects = val, true); // Forge-Change
    }

    /// <summary>
    ///     Forge-Change: cheap pre-filter so the scan does not enqueue planetmap grids,
    ///     grids parented to a planetmap, or immune grids. Cuts the candidate queue down
    ///     to actual abandoned-grid candidates before the expensive per-item checks run.
    /// </summary>
    protected override bool ShouldEnqueue(EntityUid uid)
    {
        if (_immuneQuery.HasComp(uid) || _mapCompQuery.HasComp(uid))
            return false;

        if (!_xformQuery.TryGetComponent(uid, out var xform))
            return false;

        if (_gridQuery.HasComp(xform.ParentUid))
            return false;

        return true;
    }

    protected override bool ShouldEntityCleanup(EntityUid uid)
    {
        var xform = Transform(uid);
        // if we somehow lost it
        if (!TryComp<MapGridComponent>(uid, out var grid) || !TryComp<PhysicsComponent>(uid, out var body))
            return false;

        var state = EnsureComp<GridCleanupGridComponent>(uid);
        var tileCount = CountTiles(uid, grid);

        // Forge-Change
        // If a grid lost all of its tiles, delete it immediately so stale
        // systems/components such as IFF cannot linger on an empty grid shell.
        if (tileCount == 0 || body.FixturesMass <= 0f)
            return true;

        if (TryFragmentFastPath(uid, state, xform, tileCount, out var fastDelete))
            return fastDelete;

        state.FastPathEligibleSince = null;

        var tiles = body.FixturesMass / ShuttleSystem.TileDensityMultiplier;
        var scale = MathF.Min(tiles / _aggressiveTiles, 1f);

        if ((_iffProtects && !state.IgnoreIFF && TryComp<IFFComponent>(uid, out var iff) && (iff.Flags & IFFFlags.HideLabel) == 0) // Forge-Change: IFF optional
            || CleanupHelper.HasNearbyPlayers(xform.Coordinates, state.DistanceOverride ?? _maxDistance * scale * scale) // square it
            || !state.IgnorePowered && HasPoweredAPC((uid, xform)) // don't delete if it has powered APCs
            || !state.IgnorePrice && _pricing.AppraiseGrid(uid) > _maxValue) // expensive to run, put last
        {
            state.CleanupAccumulator = TimeSpan.FromSeconds(0);
            return false;
        }
        // see if we should update timer or just be deleted
        else if (state.CleanupAccumulator < _duration)
        {
            state.CleanupAccumulator += _cleanupInterval * state.CleanupAcceleration / scale;
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Micro-fragments (few tiles, unpowered, cheap): ignore IFF, shorter real-time wait, tighter player radius.
    /// </summary>
    private bool TryFragmentFastPath(
        EntityUid uid,
        GridCleanupGridComponent state,
        TransformComponent xform,
        int tileCount,
        out bool shouldDelete)
    {
        shouldDelete = false;

        if (tileCount > _fragmentMaxTiles)
        {
            state.FastPathEligibleSince = null;
            return false;
        }

        if (!state.IgnorePowered && HasPoweredAPC((uid, xform))
            || !state.IgnorePrice && _pricing.AppraiseGrid(uid) > _fragmentMaxValue)
        {
            state.FastPathEligibleSince = null;
            return false;
        }

        if (CleanupHelper.HasNearbyPlayers(xform.Coordinates, state.DistanceOverride ?? _fragmentDistance))
        {
            state.FastPathEligibleSince = null;
            return true;
        }

        var eligibleSince = state.FastPathEligibleSince ??= _timing.CurTime;
        if (_timing.CurTime - eligibleSince < _fragmentDuration)
            return true;

        shouldDelete = true;
        return true;
    }

    private int CountTiles(EntityUid uid, MapGridComponent grid)
    {
        var count = 0;
        var enumerator = _map.GetAllTilesEnumerator(uid, grid);
        while (enumerator.MoveNext(out _))
        {
            count++;
            if (count > _fragmentMaxTiles)
                return count;
        }

        return count;
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
