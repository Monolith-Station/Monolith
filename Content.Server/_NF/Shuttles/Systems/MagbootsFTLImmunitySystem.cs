using Content.Server._NF.Shuttles.Components;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle.Components;
using Robust.Shared.Containers;

namespace Content.Server._NF.Shuttles.Systems;

/// <summary>
/// This system adds FTL knockdown immunity to entities wearing active magboots.
/// </summary>
public sealed class MagbootsFTLImmunitySystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    // Track entities we've already processed to avoid redundant work
    private readonly HashSet<EntityUid> _processedEntities = new();

    // Track the previous state of entities to detect changes
    private readonly Dictionary<EntityUid, bool> _previousImmunityState = new();

    // Query for active magboots
    private EntityQuery<ItemToggleComponent> _toggleQuery;

    public override void Initialize()
    {
        base.Initialize();

        // Set up our query
        _toggleQuery = GetEntityQuery<ItemToggleComponent>();
    }

    /// <summary>
    /// Updates the FTL knockdown immunity status for an entity, but only if the state has changed.
    /// </summary>
    private void UpdateFTLImmunity(EntityUid entity, bool shouldHaveImmunity)
    {
        // Check if the state has actually changed to avoid unnecessary work
        if (_previousImmunityState.TryGetValue(entity, out var previousState) && previousState == shouldHaveImmunity)
            return;

        // Update the tracked state
        _previousImmunityState[entity] = shouldHaveImmunity;

        if (shouldHaveImmunity)
        {
            EnsureComp<FTLKnockdownImmuneComponent>(entity);
        }
        else
        {
            RemComp<FTLKnockdownImmuneComponent>(entity);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Clear our tracking to start fresh each update
        _processedEntities.Clear();

        // Track which entities should have immunity this update
        var entitiesWithImmunity = new HashSet<EntityUid>();

        // Find all magboots components
        var query = EntityQueryEnumerator<MagbootsComponent>();
        while (query.MoveNext(out var uid, out var magboots))
        {
            // Only process each entity once per update
            if (!_processedEntities.Add(uid))
                continue;

            // Check if magboots are active
            bool isActive = _toggleQuery.TryGetComponent(uid, out var toggle) && toggle.Activated;

            // Find the entity wearing the magboots (if any)
            if (TryGetWearer(uid, magboots, out var wearer))
            {
                if (isActive)
                {
                    entitiesWithImmunity.Add(wearer);
                }
                // Apply or remove immunity based on magboots active state
                UpdateFTLImmunity(wearer, isActive);
            }
        }

        // Clean up immunity state for entities that no longer should have it
        var toRemove = new List<EntityUid>();
        foreach (var (entity, hadImmunity) in _previousImmunityState)
        {
            // If entity no longer exists or no longer should have immunity, clean it up
            if (!EntityManager.EntityExists(entity) || (!entitiesWithImmunity.Contains(entity) && hadImmunity))
            {
                if (EntityManager.EntityExists(entity))
                    RemComp<FTLKnockdownImmuneComponent>(entity);
                toRemove.Add(entity);
            }
        }

        // Remove cleaned up entities from tracking
        foreach (var entity in toRemove)
        {
            _previousImmunityState.Remove(entity);
        }
    }

    /// <summary>
    /// Helper method to find the entity wearing the magboots.
    /// </summary>
    private bool TryGetWearer(EntityUid uid, MagbootsComponent component, out EntityUid wearer)
    {
        wearer = default;

        // Find the container the magboots are in (if any)
        if (!_container.TryGetContainingContainer(uid, out var container))
            return false;

        // Check if container is an inventory slot on an entity
        if (!_inventory.TryGetContainerSlotEnumerator(container.Owner, out var enumerator))
            return false;

        // Find the specific slot that should contain these magboots
        while (enumerator.MoveNext(out var slot))
        {
            if (slot.ID == component.Slot && _container.TryGetContainer(container.Owner, slot.ID, out var slotContainer))
            {
                if (slotContainer.Contains(uid))
                {
                    wearer = container.Owner;
                    return true;
                }
            }
        }

        return false;
    }
}
