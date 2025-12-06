using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using System.Linq;
using System.Numerics;
using Content.Shared._Mono.Traits.Physical;
using Robust.Shared.Map;

namespace Content.Server._Mono.Traits.Physical;

/// <summary>
/// Handles replacing arms with JWL arms on spawn for entities with PrybarProstheticsComponent.
/// </summary>
public sealed class PrybarProstheticsSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrybarProstheticsComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<PrybarProstheticsComponent> ent, ref ComponentStartup args)
    {
        ReplaceArms(ent);
    }

    private void ReplaceArms(Entity<PrybarProstheticsComponent> ent)
    {
        if (!TryComp(ent, out BodyComponent? body))
        {
            return;
        }

        if (body.RootContainer.ContainedEntities.Count == 0)
        {
            return;
        }

        var torso = body.RootContainer.ContainedEntities.FirstOrDefault();

        if (!TryComp(torso, out BodyPartComponent? torsoPart))
        {
            return;
        }

        var leftArmSlotId = SharedBodySystem.GetPartSlotContainerId("left arm");

        if (_containerSystem.TryGetContainer(torso, leftArmSlotId, out var leftArmContainer) && leftArmContainer.ContainedEntities.Count > 0)
        {
            foreach (var leftArm in leftArmContainer.ContainedEntities.ToArray())
            {
                if (TryComp(leftArm, out BodyPartComponent? leftArmPart))
                {
                    SpawnAndReplace("JawsOfLifeLeftArm", torso, "left arm");
                }
            }
        }

        var rightArmSlotId = SharedBodySystem.GetPartSlotContainerId("right arm");

        if (_containerSystem.TryGetContainer(torso, rightArmSlotId, out var rightArmContainer) && rightArmContainer.ContainedEntities.Count > 0)
        {
            foreach (var rightArm in rightArmContainer.ContainedEntities.ToArray())
            {
                if (TryComp(rightArm, out BodyPartComponent? rightArmPart))
                {
                    SpawnAndReplace("JawsOfLifeRightArm", torso, "right arm");
                }
            }
        }
    }

    private void SpawnAndReplace(string partProtoId, EntityUid parentEntity, string slotId)
    {
        if (!_prototypeManager.TryIndex(partProtoId, out _))
        {
            return;
        }

        if (!TryComp(parentEntity, out BodyPartComponent? parentPart))
            return;

        var containerId = SharedBodySystem.GetPartSlotContainerId(slotId);

        if (_containerSystem.TryGetContainer(parentEntity, containerId, out var container))
        {
            var oldEntities = container.ContainedEntities.ToArray();

            foreach (var oldEntity in oldEntities)
            {
                if (TryComp(oldEntity, out BodyPartComponent? oldPart))
                {
                    DeleteChildParts(oldEntity, oldPart);
                }
            }

            foreach (var entity in oldEntities)
            {
                _containerSystem.Remove(entity, container);
                QueueDel(entity);
            }
        }

        var newPart = Spawn(partProtoId, new EntityCoordinates(parentEntity, Vector2.Zero));

        if (!TryComp(newPart, out BodyPartComponent? newPartComp))
        {
            QueueDel(newPart);
            return;
        }

        if (!_bodySystem.AttachPart(parentEntity, slotId, newPart, parentPart, newPartComp))
        {
            QueueDel(newPart);
        }
    }

    private void DeleteChildParts(EntityUid parent, BodyPartComponent part)
    {
        foreach (var (slotId, _) in part.Children)
        {
            var childContainerId = SharedBodySystem.GetPartSlotContainerId(slotId);

            if (_containerSystem.TryGetContainer(parent, childContainerId, out var childContainer))
            {
                var children = childContainer.ContainedEntities.ToArray();

                foreach (var child in children)
                {
                    if (TryComp(child, out BodyPartComponent? childPart))
                    {
                        DeleteChildParts(child, childPart);
                    }

                    _containerSystem.Remove(child, childContainer);

                    QueueDel(child);
                }
            }
        }
    }
}
