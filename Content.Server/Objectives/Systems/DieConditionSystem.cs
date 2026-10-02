using Content.Server.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;

namespace Content.Server.Objectives.Systems;

public sealed partial class DieConditionSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DieConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, DieConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        // Mono - Check for mind, to slightly less hardcode it. I need objectives for other things too!
        if (TryComp<MindComponent>(uid, out var mindComponent))
            args.Progress = _mind.IsCharacterDeadIc(mindComponent) ? 1f : 0f;
    }
}
