using Content.Shared.Gravity;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels.Gravity;

public sealed partial class CEAutoGridGravitySystem : EntitySystem
{
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEAutoGridGravityComponent, MapInitEvent>(OnComponentInit);
        SubscribeLocalEvent<GridInitializeEvent>(OnGridInit);
    }

    // Fires when the component is added to the map entity.
    // If the map is already initialized (zLevelsComponentOverrides flow), iterate existing grids.
    // If not yet initialized, GridInitializeEvent handles each grid as it comes up.
    private void OnComponentInit(Entity<CEAutoGridGravityComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<MapComponent>(ent, out var mapComp) || !_map.IsInitialized(ent.Owner))
            return;

        foreach (var grid in _mapManager.GetAllGrids(mapComp.MapId))
        {
            EnableGravity(grid.Owner);
        }

        EnableGravity(ent);
    }

    // Fires for every grid that initializes. Handles both map-load time (component already on map)
    // and runtime grid spawning (e.g. shuttles arriving).
    private void OnGridInit(GridInitializeEvent ev)
    {
        var mapUid = Transform(ev.EntityUid).MapUid;
        if (mapUid == null || !HasComp<CEAutoGridGravityComponent>(mapUid.Value))
            return;

        EnableGravity(ev.EntityUid);
    }

    private void EnableGravity(EntityUid ent)
    {
        var gravity = EnsureComp<GravityComponent>(ent);
        gravity.Inherent = true;
        gravity.Enabled = true;
        Dirty(ent, gravity);
    }
}
