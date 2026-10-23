using Content.Server.Objectives.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Mind;

namespace Content.Server.Objectives.Systems;

/// <summary>
/// Handles progress for the survive objective condition.
/// </summary>
public sealed partial class SurviveConditionSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurviveConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, SurviveConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        // Mono - Check for mind, to slightly less hardcode it. I need objectives for other things too!
        if (TryComp<MindComponent>(uid, out var mind))
            args.Progress = _mind.IsCharacterDeadIc(mind) ? 0f : 1f;
    }
}
