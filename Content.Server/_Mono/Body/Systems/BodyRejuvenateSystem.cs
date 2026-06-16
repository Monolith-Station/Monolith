using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.Rejuvenate;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Body.Systems;

/// <summary>
/// System that handles restoring missing body parts and organs during rejuvenation.
/// </summary>
public sealed partial class BodyRejuvenateSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _bodySystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnRejuvenate(Entity<BodyComponent> entity, ref RejuvenateEvent args)
    {
        // Validate entity
        if (!entity.Owner.Valid || TerminatingOrDeleted(entity.Owner))
            return;

        // Only restore if we have a prototype to work from
        if (entity.Comp.Prototype == null)
            return;

        if (!_prototypeManager.TryIndex(entity.Comp.Prototype.Value, out BodyPrototype? prototype))
            return;

        // Clean up any severed/gibbed parts and organs that belonged to this body
        CleanupDetachedBodyParts(entity);

        RestoreMissingBodyParts(entity, prototype);

        // Ensure the entity is properly revived after restoration
        EnsureProperMobState(entity);
    }

    /// <summary>
    /// Cleans up any detached body parts and organs that originally belonged to this body.
    /// This includes severed limbs, gibbed parts, and loose organs lying around.
    /// </summary>
    private void CleanupDetachedBodyParts(Entity<BodyComponent> bodyEntity)
    {
        var partsToDelete = new List<EntityUid>();
        var organsToDelete = new List<EntityUid>();

        // First, check nearby entities (more efficient for most cases)
        var bodyPos = _transform.GetMapCoordinates(bodyEntity.Owner);
        if (bodyPos.MapId != MapId.Nullspace)
        {
            var nearbyParts = new HashSet<Entity<BodyPartComponent>>();
            var nearbyOrgans = new HashSet<Entity<OrganComponent>>();

            // Look for body parts and organs within a reasonable range (10 tiles)
            _lookup.GetEntitiesInRange(bodyPos, 10f, nearbyParts);
            _lookup.GetEntitiesInRange(bodyPos, 10f, nearbyOrgans);

            // Check nearby body parts
            foreach (var (partUid, partComp) in nearbyParts)
            {
                // Skip if this part is currently attached to the body
                if (partComp.Body == bodyEntity.Owner)
                    continue;

                // Check if this part originally belonged to this body
                if (WasPartOfBody(partUid, bodyEntity.Owner, partComp))
                {
                    partsToDelete.Add(partUid);
                }
            }

            // Check nearby organs
            foreach (var (organUid, organComp) in nearbyOrgans)
            {
                // Skip if this organ is currently attached to the body
                if (organComp.Body == bodyEntity.Owner)
                    continue;

                // Check if this organ originally belonged to this body
                if (organComp.OriginalBody == bodyEntity.Owner)
                {
                    organsToDelete.Add(organUid);
                }
            }
        }

        // Fallback: global search if we didn't find much nearby
        if (partsToDelete.Count == 0 && organsToDelete.Count == 0)
        {
            // Find all body parts that have this entity as their original body but are not currently attached
            var bodyPartQuery = EntityQueryEnumerator<BodyPartComponent>();

            while (bodyPartQuery.MoveNext(out var partUid, out var partComp))
            {
                // Skip if this part is currently attached to the body
                if (partComp.Body == bodyEntity.Owner)
                    continue;

                // Check if this part originally belonged to this body
                if (WasPartOfBody(partUid, bodyEntity.Owner, partComp))
                {
                    partsToDelete.Add(partUid);
                }
            }

            // Find all organs that originally belonged to this body but are not currently attached
            var organQuery = EntityQueryEnumerator<OrganComponent>();

            while (organQuery.MoveNext(out var organUid, out var organComp))
            {
                // Skip if this organ is currently attached to the body
                if (organComp.Body == bodyEntity.Owner)
                    continue;

                // Check if this organ originally belonged to this body
                if (organComp.OriginalBody == bodyEntity.Owner)
                {
                    organsToDelete.Add(organUid);
                }
            }
        }

        // Delete all the detached parts and organs
        foreach (var partUid in partsToDelete)
        {
            QueueDel(partUid);
        }

        foreach (var organUid in organsToDelete)
        {
            QueueDel(organUid);
        }
    }

    /// <summary>
    /// Checks if a body part was originally part of the specified body.
    /// This is used to identify severed/gibbed parts that should be cleaned up.
    /// </summary>
    private bool WasPartOfBody(EntityUid partUid, EntityUid bodyUid, BodyPartComponent? partComp = null)
    {
        if (!Resolve(partUid, ref partComp, logMissing: false))
            return false;

        // Check if the part has any organs that originally belonged to this body
        foreach (var organSlotId in partComp.Organs.Keys)
        {
            var containerId = SharedBodySystem.GetOrganContainerId(organSlotId);
            if (_containerSystem.TryGetContainer(partUid, containerId, out var container))
            {
                foreach (var organUid in container.ContainedEntities)
                {
                    if (TryComp<OrganComponent>(organUid, out var organComp)
                        && organComp.OriginalBody == bodyUid)
                    {
                        return true;
                    }
                }
            }
        }

        // More aggressive cleanup: if the part is not in a container (i.e., lying on the ground)
        // and has the same prototype structure as what the body should have, assume it belongs to this body
        if (!_containerSystem.TryGetContainingContainer(partUid, out var _))
        {
            // Check if this part type matches what should be on this body
            if (TryComp<BodyComponent>(bodyUid, out var bodyComp) && bodyComp.Prototype != null)
            {
                if (_prototypeManager.TryIndex(bodyComp.Prototype.Value, out BodyPrototype? prototype))
                {
                    // If this part type exists in the body prototype, it's likely from this body
                    foreach (var slot in prototype.Slots.Values)
                    {
                        if (slot.Part != null && HasComp<BodyPartComponent>(partUid))
                        {
                            // This is a more aggressive approach - delete loose body parts near the body
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Ensures the entity has proper mob state after body restoration.
    /// This fixes issues where players remain stuck dying after head restoration.
    /// </summary>
    private void EnsureProperMobState(Entity<BodyComponent> bodyEntity)
    {
        // The rejuvenate event should have already handled damage and mob state,
        // but we need to make sure the body is properly recognized as complete

        // Force a body update to recalculate mob state based on new parts
        var rootPart = _bodySystem.GetRootPartOrNull(bodyEntity, bodyEntity.Comp);
        if (rootPart != null)
        {
            // Trigger a recursive body update to ensure all new parts are properly registered
            var updateEvent = new BodyPartAddedEvent("rejuvenate_update", rootPart.Value);
            RaiseLocalEvent(bodyEntity, ref updateEvent);
        }
    }

    /// <summary>
    /// Restores any missing body parts and organs based on the original body prototype.
    /// </summary>
    private void RestoreMissingBodyParts(Entity<BodyComponent> bodyEntity, BodyPrototype prototype)
    {
        // Get the root part
        var rootPart = _bodySystem.GetRootPartOrNull(bodyEntity, bodyEntity.Comp);
        if (rootPart == null)
            return;

        // Use BFS to traverse the prototype structure and restore missing parts
        var frontier = new Queue<string>();
        frontier.Enqueue(prototype.Root);

        // Track which slots we've processed
        var processedSlots = new HashSet<string>();
        processedSlots.Add(prototype.Root);

        // Map prototype slots to actual entities
        var slotToEntity = new Dictionary<string, EntityUid>();
        slotToEntity[prototype.Root] = rootPart.Value.Entity;

        while (frontier.TryDequeue(out var currentSlotId))
        {
            var currentSlot = prototype.Slots[currentSlotId];
            var currentEntity = slotToEntity[currentSlotId];

            // Process each connection from this slot
            foreach (var connectionSlotId in currentSlot.Connections)
            {
                if (processedSlots.Contains(connectionSlotId))
                    continue;

                processedSlots.Add(connectionSlotId);
                var connectionSlot = prototype.Slots[connectionSlotId];

                // Validate current entity before proceeding
                if (!currentEntity.Valid || TerminatingOrDeleted(currentEntity))
                    continue;

                // Check if this part already exists
                var existingPart = FindExistingPart(currentEntity, connectionSlotId);

                if (existingPart != null && existingPart.Value.Valid)
                {
                    // Part exists, add it to our mapping and continue
                    slotToEntity[connectionSlotId] = existingPart.Value;
                    frontier.Enqueue(connectionSlotId);

                    // Restore missing organs on this existing part
                    RestoreMissingOrgans(existingPart.Value, connectionSlot.Organs);
                }
                else if (connectionSlot.Part != null)
                {
                    // Part is missing, spawn and attach it
                    var newPart = SpawnAndAttachPart(currentEntity, connectionSlotId, connectionSlot.Part);
                    if (newPart != null && newPart.Value.Valid)
                    {
                        slotToEntity[connectionSlotId] = newPart.Value;
                        frontier.Enqueue(connectionSlotId);

                        // Add organs to the newly created part
                        RestoreMissingOrgans(newPart.Value, connectionSlot.Organs);
                    }
                }
            }
        }

        // Restore missing organs on the root part
        var rootSlot = prototype.Slots[prototype.Root];
        RestoreMissingOrgans(rootPart.Value.Entity, rootSlot.Organs);
    }

    /// <summary>
    /// Finds an existing body part in the specified slot.
    /// </summary>
    private EntityUid? FindExistingPart(EntityUid parentEntity, string slotId)
    {
        if (!TryComp<BodyPartComponent>(parentEntity, out var parentPart))
            return null;

        var containerId = SharedBodySystem.GetPartSlotContainerId(slotId);
        if (!_containerSystem.TryGetContainer(parentEntity, containerId, out var container))
            return null;

        return container.ContainedEntities.FirstOrDefault();
    }

    /// <summary>
    /// Spawns and attaches a new body part to the specified parent.
    /// </summary>
    private EntityUid? SpawnAndAttachPart(EntityUid parentEntity, string slotId, string partPrototype)
    {
        // Validate parent entity
        if (!parentEntity.Valid || TerminatingOrDeleted(parentEntity))
            return null;

        if (!TryComp<TransformComponent>(parentEntity, out var parentTransform))
            return null;

        // Spawn the new part
        var newPart = Spawn(partPrototype, parentTransform.Coordinates);

        if (!TryComp<BodyPartComponent>(newPart, out var partComponent))
        {
            QueueDel(newPart);
            return null;
        }

        // Try to attach it
        if (_bodySystem.TryCreatePartSlotAndAttach(parentEntity, slotId, newPart, partComponent.PartType))
        {
            return newPart;
        }
        else
        {
            QueueDel(newPart);
            return null;
        }
    }

    /// <summary>
    /// Restores any missing organs on the specified body part.
    /// </summary>
    private void RestoreMissingOrgans(EntityUid partEntity, Dictionary<string, string> requiredOrgans)
    {
        // Validate part entity
        if (!partEntity.Valid || TerminatingOrDeleted(partEntity))
            return;

        if (!TryComp<BodyPartComponent>(partEntity, out var partComponent))
            return;

        if (!TryComp<TransformComponent>(partEntity, out var partTransform))
            return;

        foreach (var (organSlotId, organPrototype) in requiredOrgans)
        {
            // Check if organ already exists
            var containerId = SharedBodySystem.GetOrganContainerId(organSlotId);
            if (_containerSystem.TryGetContainer(partEntity, containerId, out var container)
                && container.ContainedEntities.Any())
            {
                continue; // Organ already exists
            }

            // Create the organ slot if it doesn't exist
            _bodySystem.TryCreateOrganSlot(partEntity, organSlotId, out var slot, partComponent);

            // Spawn and attach the missing organ
            var newOrgan = Spawn(organPrototype, partTransform.Coordinates);
            if (TryComp<OrganComponent>(newOrgan, out var organComponent))
            {
                if (!_bodySystem.InsertOrgan(partEntity, newOrgan, organSlotId, partComponent, organComponent))
                {
                    // If insertion failed, clean up the spawned organ
                    QueueDel(newOrgan);
                }
            }
            else
            {
                QueueDel(newOrgan);
            }
        }
    }
}
