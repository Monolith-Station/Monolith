using System.Linq;
using Content.Shared._Mono;
using Content.Shared._Mono.NoHack;
using Content.Shared._Mono.NoDeconstruct;
using Content.Shared.Doors.Components;
using Content.Shared.VendingMachines;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;

namespace Content.Server._Mono;

/// <summary>
/// System that handles the GridRaiderComponent, which applies NoHack and NoDeconstruct to entities with Door and/or VendingMachine components on a grid.
/// Protection is applied once during initialization and remains until the component is removed.
/// </summary>
public sealed class GridRaiderSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GridRaiderComponent, MapInitEvent>(OnGridRaiderMapInit);
        SubscribeLocalEvent<GridRaiderComponent, ComponentShutdown>(OnGridRaiderShutdown);
    }



    private void OnGridRaiderMapInit(EntityUid uid, GridRaiderComponent component, MapInitEvent args)
    {
        // Verify this is applied to a grid
        if (!HasComp<MapGridComponent>(uid))
        {
            Log.Warning($"GridRaiderComponent applied to non-grid entity {ToPrettyString(uid)}");
            return;
        }

        // Find all entities on the grid and apply NoHack/NoDeconstruct to doors and vending machines
        ApplyInitialProtection(uid, component);
    }

    private void OnGridRaiderShutdown(EntityUid uid, GridRaiderComponent component, ComponentShutdown args)
    {
        // When the component is removed, remove NoHack/NoDeconstruct from all protected entities
        foreach (var entity in component.ProtectedEntities.ToList())
        {
            if (EntityManager.EntityExists(entity))
            {
                RemoveProtection(entity);
            }
        }

        component.ProtectedEntities.Clear();
    }





    /// <summary>
    /// Applies initial protection to all eligible entities on the grid during map initialization
    /// </summary>
    private void ApplyInitialProtection(EntityUid gridUid, GridRaiderComponent component)
    {
        // Get all entities currently on the grid
        var allEntitiesOnGrid = _lookup.GetEntitiesIntersecting(gridUid).ToHashSet();

        // Find entities that should be protected based on component settings
        foreach (var entity in allEntitiesOnGrid)
        {
            // Skip the grid itself and entities inside containers
            if (entity == gridUid || _container.IsEntityInContainer(entity))
                continue;

            // Check if this entity should be protected based on current settings
            var shouldProtect = false;
            var hackProtect = true;

            if (component.ProtectDoors && HasComp<DoorComponent>(entity))
                shouldProtect = true;

            if (component.ProtectVendingMachines && HasComp<VendingMachineComponent>(entity))
            {
                shouldProtect = true;
                hackProtect = false; // vendors can be hackable
            }

            if (shouldProtect)
                ApplyProtection(entity, component, hackProtect);
        }
    }

    /// <summary>
    /// Applies NoHack and NoDeconstruct to an entity and adds it to the protected entities list
    /// </summary>
    private void ApplyProtection(EntityUid entityUid, GridRaiderComponent component, bool hackProtect = true, bool deconProtect = true)
    {
        // Skip if the entity is already protected
        if (component.ProtectedEntities.Contains(entityUid))
            return;

        // Apply NoHack and NoDeconstruct components
        if (hackProtect)
            EnsureComp<NoHackComponent>(entityUid);
        if (deconProtect)
            EnsureComp<NoDeconstructComponent>(entityUid);

        component.ProtectedEntities.Add(entityUid);
    }

    /// <summary>
    /// Removes NoHack and NoDeconstruct from an entity
    /// </summary>
    private void RemoveProtection(EntityUid entityUid)
    {
        if (HasComp<NoHackComponent>(entityUid))
        {
            RemComp<NoHackComponent>(entityUid);
        }

        if (HasComp<NoDeconstructComponent>(entityUid))
        {
            RemComp<NoDeconstructComponent>(entityUid);
        }
    }


}
