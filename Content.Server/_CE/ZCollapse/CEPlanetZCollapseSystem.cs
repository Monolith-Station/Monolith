using Content.Server._Mono.Planets;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._FarHorizons.StarSystem;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZCollapse;

public sealed partial class CEPlanetZCollapseSystem : EntitySystem
{
    [Dependency] private EntityQuery<MapComponent> _mapQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private EntityQuery<CEZMapComponent> _zMapQuery = default!;
    [Dependency] private EntityQuery<PlanetMapGenerationComponent> _planetQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    private readonly HashSet<EntityUid> _cleanup = new();

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(CEZCollapseSystem));
        SubscribeLocalEvent<PlanetSurfaceComponent, ComponentStartup>(OnPlanetSurfaceStartup);
        SubscribeLocalEvent<CEGridStabilityComponent, ComponentStartup>(OnStabilityStartup);
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var uid in _cleanup)
        {
            if (IsPlanetChildGrid(uid))
                RemComp<CEGridStabilityComponent>(uid);
        }

        _cleanup.Clear();
    }

    private void OnPlanetSurfaceStartup(Entity<PlanetSurfaceComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<PlanetMapGenerationComponent>(ent.Owner, out var planet))
            return;

        foreach (var (depth, mapUid) in planet.Layers)
        {
            if (!_mapQuery.HasComponent(mapUid) || !_gridQuery.HasComponent(mapUid))
                continue;

            EnsureComp<CEGridStabilityComponent>(mapUid).SupportLowestLevel = depth <= 0;
        }
    }

    private void OnStabilityStartup(Entity<CEGridStabilityComponent> ent, ref ComponentStartup args)
    {
        if (IsPlanetChildGrid(ent.Owner))
            _cleanup.Add(ent.Owner);
    }

    private void OnGridSplit(ref GridSplitEvent args)
    {
        foreach (var uid in args.NewGrids)
            _cleanup.Add(uid);
    }

    private bool IsPlanetChildGrid(EntityUid uid)
    {
        if (_mapQuery.HasComponent(uid) ||
            !_gridQuery.HasComponent(uid) ||
            !_xformQuery.TryGetComponent(uid, out var xform) ||
            xform.MapUid is not { } mapUid ||
            !_zMapQuery.TryGetComponent(mapUid, out var zMap))
            return false;

        return _planetQuery.HasComponent(zMap.NetworkUid);
    }
}
