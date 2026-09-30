using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Traits.Physical;

/// <summary>
/// Cancels step triggers for entities that have TrapAvoiderComponent, unless the owner of the trigger has the UnavoidableTrap tag.
/// </summary>
public sealed class TrapAvoiderSystem : EntitySystem
{
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> UnavoidableTag = "UnavoidableTrap";

    public override void Initialize()
    {
        SubscribeLocalEvent<StepTriggerComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
    }

    private void OnStepTriggerAttempt(Entity<StepTriggerComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (!_tag.HasTag(ent.Owner, UnavoidableTag) && HasComp<TrapAvoiderComponent>(args.Tripper))
            args.Cancelled = true;
    }
}
