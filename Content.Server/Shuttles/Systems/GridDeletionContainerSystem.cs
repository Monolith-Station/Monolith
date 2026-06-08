using System.Linq;
using Content.Server.Storage.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Shuttles.Systems;

/// <summary>
/// This system ensures that when a grid is deleted, all entities attached to it (directly or indirectly,
/// including those inside containers) are also deleted.
/// This fixes an issue where entities inside containers were left behind in space after grid deletion.
/// </summary>
public sealed partial class GridDeletionContainerSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    // Track grids currently being processed to prevent re-entrancy issues.
    private readonly HashSet<EntityUid> _gridsBeingDeleted = new();

    public override void Initialize()
    {
        base.Initialize();
        // Primarily rely on EntityTerminatingEvent as the trigger.
        SubscribeLocalEvent<MapGridComponent, EntityTerminatingEvent>(OnGridTerminating);
    }

    private void OnGridTerminating(EntityUid uid, MapGridComponent component, ref EntityTerminatingEvent args)
    {
        // Prevent re-entrancy. If we are already processing this grid, stop.
        if (!_gridsBeingDeleted.Add(uid))
            return;

        try
        {
            // Use a HashSet to track entities processed during *this specific* grid deletion event
            // to avoid cycles and redundant work within the recursive calls.
            var processedEntities = new HashSet<EntityUid>();

            Logger.Debug($"Grid {ToPrettyString(uid)} is terminating. Ensuring all child entities are deleted recursively.");

            // Start the recursive deletion process for all direct transform children of the grid.
            // We don't process the grid itself (uid) initially because it's already terminating.
            if (TryComp<TransformComponent>(uid, out var gridXform))
            {
                // Get the children of the transform using ChildEnumerator
                var childEnumerator = gridXform.ChildEnumerator;

                // Convert to an array to avoid modification issues during iteration
                var children = new List<EntityUid>();
                while (childEnumerator.MoveNext(out var child))
                {
                    children.Add(child);
                }

                // Process each child
                foreach (var childUid in children)
                {
                    EnsureContainedEntitiesAreDeleted(childUid, uid, processedEntities);
                }
            }

            Logger.Debug($"Finished recursive deletion processing for terminating grid {ToPrettyString(uid)}. Processed entity count (excluding grid): {processedEntities.Count - 1}"); // Exclude the grid itself if it got added
        }
        finally
        {
            // Ensure we remove the grid from the tracking set once processing is complete or if an error occurs.
            _gridsBeingDeleted.Remove(uid);
        }
    }

    /// <summary>
    /// Recursively ensures an entity and all its descendants (in transform hierarchy and containers)
    /// are queued for deletion.
    /// </summary>
    /// <param name="entity">The entity to process.</param>
    /// <param name="rootGridUid">The original grid that is terminating.</param>
    /// <param name="processedEntities">Set tracking entities already processed in this deletion event.</param>
    private void EnsureContainedEntitiesAreDeleted(EntityUid entity, EntityUid rootGridUid, HashSet<EntityUid> processedEntities)
    {
        // 1. Check if already processed or if the entity doesn't exist anymore.
        // We also skip the root grid itself as it's handled by the engine's termination process.
        if (entity == rootGridUid || !Exists(entity) || !processedEntities.Add(entity))
            return;

        // 2. Recursively process children within containers FIRST.
        if (TryComp<ContainerManagerComponent>(entity, out var containerManager))
        {
            foreach (var container in containerManager.Containers.Values)
            {
                // Iterate over a copy as EnsureContainedEntitiesAreDeleted might modify the container via QueueDel.
                foreach (var contained in container.ContainedEntities.ToArray())
                {
                    EnsureContainedEntitiesAreDeleted(contained, rootGridUid, processedEntities);
                }
            }
        }

        // Fallback for EntityStorage: Ensure its flag is set, just in case.
        // This is defense-in-depth; the recursive deletion should handle contents regardless.
        if (TryComp<EntityStorageComponent>(entity, out var storageComp))
        {
            storageComp.DeleteContentsOnDestruction = true;
            Dirty(entity, storageComp);
        }

        // 3. Recursively process transform children SECOND.
        if (TryComp<TransformComponent>(entity, out var xform))
        {
            // Get the children of the transform using ChildEnumerator
            var childEnumerator = xform.ChildEnumerator;

            // Convert to an array to avoid modification issues during iteration
            var children = new List<EntityUid>();
            while (childEnumerator.MoveNext(out var child))
            {
                children.Add(child);
            }

            // Process each child
            foreach (var childUid in children)
            {
                EnsureContainedEntitiesAreDeleted(childUid, rootGridUid, processedEntities);
            }
        }

        // 4. Queue the current entity for deletion AFTER its children have been processed.
        // We check Exists again as a child's deletion process might have deleted this entity.
        // We also avoid queueing deletion during client prediction.
        if (Exists(entity) && !_timing.IsFirstTimePredicted)
        {
            // Logger.Debug($"Queueing deletion for entity {ToPrettyString(entity)} during grid {ToPrettyString(rootGridUid)} termination.");
            QueueDel(entity);
        }
    }
}
