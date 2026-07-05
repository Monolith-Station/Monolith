using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Systems;

namespace Content.Shared._Mono.Traits.Physical;

/// <summary>
/// Cancels step triggers for entities that have TrapAvoiderComponent, unless the step trigger specifies otherwise.
/// </summary>
public sealed class TrapAvoiderSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<StepTriggerComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
    }

    private void OnStepTriggerAttempt(Entity<StepTriggerComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (!ent.Comp.BypassesTrapAvoider && HasComp<TrapAvoiderComponent>(args.Tripper))
            args.Cancelled = true;
    }
}
