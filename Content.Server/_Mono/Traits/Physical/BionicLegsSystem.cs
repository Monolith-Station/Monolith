using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using System.Linq;
using System.Numerics;
using Content.Shared._Mono.Traits.Physical;
using Robust.Shared.Map;
using Content.Shared.Standing;

namespace Content.Server._Mono.Traits.Physical;

public sealed class BionicLegsSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BionicLegsComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<BionicLegsComponent> ent, ref ComponentStartup args)
    {
        ReplaceLegs(ent);
        _standing.Stand(ent.Owner, force: true);
    }

    private void ReplaceLegs(Entity<BionicLegsComponent> ent)
    {
        if (!TryComp(ent, out BodyComponent? body))
            return;

        if (body.RootContainer.ContainedEntities.Count == 0)
            return;

        var torso = body.RootContainer.ContainedEntities.FirstOrDefault();

        if (!TryComp(torso, out BodyPartComponent? torsoPart))
            return;

        var leftLegSlotId = SharedBodySystem.GetPartSlotContainerId("left leg");

        if (_containerSystem.TryGetContainer(torso, leftLegSlotId, out var leftLegContainer) && leftLegContainer.ContainedEntities.Count > 0)
        {
            foreach (var leftLeg in leftLegContainer.ContainedEntities.ToArray())
            {
                if (TryComp(leftLeg, out BodyPartComponent? leftLegPart))
                    SpawnAndReplace("SpeedLeftLeg", torso, "left leg");
            }
        }

        var rightLegSlotId = SharedBodySystem.GetPartSlotContainerId("right leg");

        if (_containerSystem.TryGetContainer(torso, rightLegSlotId, out var rightLegContainer) && rightLegContainer.ContainedEntities.Count > 0)
        {
            foreach (var rightLeg in rightLegContainer.ContainedEntities.ToArray())
            {
                if (TryComp(rightLeg, out BodyPartComponent? rightLegPart))
                    SpawnAndReplace("SpeedRightLeg", torso, "right leg");
            }
        }
    }

    private void SpawnAndReplace(string partProtoId, EntityUid parentEntity, string slotId)
    {
        if (!_prototypeManager.TryIndex(partProtoId, out _))
            return;

        if (!TryComp(parentEntity, out BodyPartComponent? parentPart))
            return;

        var containerId = SharedBodySystem.GetPartSlotContainerId(slotId);

        if (_containerSystem.TryGetContainer(parentEntity, containerId, out var container))
        {
            var oldEntities = container.ContainedEntities.ToArray();

            foreach (var oldEntity in oldEntities)
            {
                if (TryComp(oldEntity, out BodyPartComponent? oldPart))
                    DeleteChildParts(oldEntity, oldPart);
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
            QueueDel(newPart);
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
                        DeleteChildParts(child, childPart);

                    _containerSystem.Remove(child, childContainer);
                    QueueDel(child);
                }
            }
        }
    }
}


