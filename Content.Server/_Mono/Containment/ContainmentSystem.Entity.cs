using System.Linq;
using Content.Server._Mono.Containment.Components;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._Mono.Containment;
public sealed partial class ContainmentSystem
{
    private readonly List<EntityUid?> _entityRemoveQueue = new();
    private void UpdateEntity(TransformComponent xform, Entity<ContainmentComponent> containment)
    {
        foreach (var entity in containment.Comp.ActiveEntities)
        {
            if (!entity.HasValue ||
                TerminatingOrDeleted(entity) ||
                xform.Coordinates.TryDistance(EntityManager, Transform(entity.Value).Coordinates, out var distance) &&
                distance >= containment.Comp.Radius)
            {
                RemoveContainedEntity(entity);
                continue;
            }

            if (!TryComp<ContainableEntityComponent>(entity, out var containable))
                continue;

            AddPoints(GetPointOutput(entity.Value, containable, containment), containment.Owner);
            AdjustMultiplier(containable);
        }
    }

    private float GetPointOutput(EntityUid uid, ContainableEntityComponent containable, ContainmentComponent comp)
    {
        return containable.BasePoints * containable.Multiplier * HealthPenalty(uid, comp);
    }

    private void AdjustMultiplier(ContainableEntityComponent cont)
    {
        cont.Multiplier -= cont.MultiplierDecay/MathF.Sqrt(cont.Multiplier);
    }

    private float HealthPenalty(EntityUid? ent, ContainmentComponent cont)
    {
        if (!TryComp<MobThresholdsComponent>(ent, out var thresholds) ||
            !TryComp<DamageableComponent>(ent, out var damage))
            return 1f;

        if (thresholds.CurrentThresholdState is MobState.Dead or MobState.Critical) // If its dead - no points for you im sorry.
            return 0f;

        return !_threshold.TryGetDeadThreshold(ent.Value, out var deadThreshold)
            ? 1f
            : Math.Clamp((deadThreshold.Value.Float() - damage.TotalDamage.Float()) / deadThreshold.Value.Float(), cont.HealthPenalty, 1f);
    }

    private void RegisterEntities(TransformComponent xform, ContainmentComponent containment)
    {
        var entities = _lookup.GetEntitiesInRange<ContainableEntityComponent>(xform.Coordinates, containment.Radius);

        foreach (var entity in entities.Where(entity => !containment.ActiveEntities.Contains(entity)))
        {
            containment.ActiveEntities.Add(entity);
        }

        _popup.PopupCoordinates(Loc.GetString("containment-register-signal",
            ("entities_count", containment.ActiveEntities.Count)),
            xform.Coordinates);
        _audio.PlayPvs(containment.RegisterSound, xform.Coordinates);
    }

    private void RemoveContainedEntity(EntityUid? ent)
    {
        _entityRemoveQueue.Add(ent);
    }
}
