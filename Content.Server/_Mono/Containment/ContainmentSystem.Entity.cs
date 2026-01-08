using System.Linq;
using Content.Server._Mono.Containment.Components;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._Mono.Containment;
public sealed partial class ContainmentSystem
{
    private void UpdateEntity(TransformComponent xform, ContainmentComponent containment)
    {
        foreach (var entity in containment.ActiveEntities.ToArray())
        {
            if (!entity.HasValue ||
                !HasComp<TransformComponent>(entity.Value) ||
                xform.Coordinates.TryDistance(EntityManager, Transform(entity.Value).Coordinates, out var distance) &&
                distance >= containment.Radius)
            {
                RemoveContainedEntity(containment, entity);
                continue;
            }

            if (!TryComp<ContainableEntityComponent>(entity, out var cont))
                continue;

            AddPoints(cont.BasePoints * cont.Multiplier * HealthPenalty(entity, containment), containment.Owner);
            AdjustMultiplier(cont);
        }
    }

    private float GetPointOutput(ContainableEntityComponent cont, EntityUid containmentEntity, ContainmentComponent comp)
    {
        return cont.BasePoints * cont.Multiplier * HealthPenalty(containmentEntity, comp);
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

    private void RemoveContainedEntity(ContainmentComponent containment, EntityUid? ent)
    {
        containment.ActiveEntities.Remove(ent);
    }
}
