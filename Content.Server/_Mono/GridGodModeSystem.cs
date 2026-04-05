using System.Linq;
using Content.Server.Damage.Systems;
using Content.Server.NPC.HTN;
using Content.Server.Spreader;
using Content.Shared._Mono;
using Content.Shared.Damage.Components;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._Mono;

/// <summary>
/// System that handles the GridGodModeComponent, which applies GodMode to all non-organic entities on a grid.
/// </summary>
public sealed class GridGodModeSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GodmodeSystem _godmode = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GridGodModeComponent, MapInitEvent>(OnGridGodModeMapInit);
        SubscribeLocalEvent<GridGodModeComponent, ComponentShutdown>(OnGridGodModeShutdown);
    }

    private void OnGridGodModeMapInit(EntityUid uid, GridGodModeComponent component, MapInitEvent args)
    {
        // Verify this is applied to a grid
        if (!HasComp<MapGridComponent>(uid))
        {
            Log.Warning($"GridGodModeComponent applied to non-grid entity {ToPrettyString(uid)}");
            return;
        }

        // Find all entities on the grid and apply GodMode to them if they're not organic
        var allEntitiesOnGrid = _lookup.GetEntitiesIntersecting(uid).ToHashSet();

        foreach (var entity in allEntitiesOnGrid)
        {
            // Skip the grid itself
            if (entity == uid)
                continue;

            ProcessEntityOnGrid(uid, entity, component);
        }
    }

    private void OnGridGodModeShutdown(EntityUid uid, GridGodModeComponent component, ComponentShutdown args)
    {
        // When the component is removed, remove GodMode from all protected entities
        foreach (var entity in component.ProtectedEntities.ToList())
        {
            if (EntityManager.EntityExists(entity))
            {
                RemoveGodMode(entity);
            }
        }

        component.ProtectedEntities.Clear();
    }

    /// <summary>
    /// Process an entity on a grid and apply GodMode if appropriate
    /// </summary>
    private void ProcessEntityOnGrid(EntityUid gridUid, EntityUid entityUid, GridGodModeComponent component)
    {
        // Don't apply GodMode to organic entities, ghosts, npcs, or kudzu
        if (IsOrganic(entityUid) || HasComp<GhostComponent>(entityUid) || HasComp<KudzuComponent>(entityUid))
            return;

        ApplyGodMode(gridUid, entityUid, component);
    }

    /// <summary>
    /// Applies GodMode to an entity and adds it to the protected entities list
    /// </summary>
    private void ApplyGodMode(EntityUid gridUid, EntityUid entityUid, GridGodModeComponent component)
    {
        // Skip if the entity is already protected
        if (component.ProtectedEntities.Contains(entityUid))
            return;

        // Apply GodMode
        _godmode.EnableGodmode(entityUid);
        component.ProtectedEntities.Add(entityUid);
    }

    /// <summary>
    /// Removes GodMode from an entity
    /// </summary>
    private void RemoveGodMode(EntityUid entityUid)
    {
        if (HasComp<GodmodeComponent>(entityUid))
        {
            _godmode.DisableGodmode(entityUid);
        }
    }

    /// <summary>
    /// Checks if an entity is organic (i.e., has a mind or is a mob)
    /// </summary>
    private bool IsOrganic(EntityUid entityUid)
    {
        // Skip ghosts
        if (HasComp<GhostComponent>(entityUid))
            return false;

        // Check if we have a player entity that's either still around or alive and may come back
        if (_mind.TryGetMind(entityUid, out var mind, out var mindComp) &&
            (mindComp.Session != null || !_mind.IsCharacterDeadPhysically(mindComp)))
        {
            return true;
        }

        // Also consider anything with a MobStateComponent as organic
        if (HasComp<MobStateComponent>(entityUid))
        {
            return true;
        }

        // Also check for anything with HTN such as NPCs, such as turrets.
        if (HasComp<HTNComponent>(entityUid))
        {
            return true;
        }

        return false;
    }
}
