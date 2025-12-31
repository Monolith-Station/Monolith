using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Construction.Components;
using Robust.Shared.Map;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server._Mono.NPC.HTN.Operators;

/// <summary>
/// Makes guns of parent shuttle fire at specified target key. Hands the targeting off to ShipTargetingSystem.
/// </summary>
public sealed partial class ShipFireGunsOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private ShipTargetingSystem _targeting = default!;

    /// <summary>
    /// When to shut the task down.
    /// </summary>
    [DataField]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    /// <summary>
    /// When this operator finishes, should we remove the target key?
    /// </summary>
    [DataField]
    public bool RemoveKeyOnFinish = true;

    /// <summary>
    /// Target EntityCoordinates to shoot at.
    /// </summary>
    [DataField]
    public string TargetKey = "ShipTargetCoordinates";

    /// <summary>
    /// How good to lead the target.
    /// </summary>
    [DataField]
    public float LeadingAccuracy = 1f;

    /// <summary>
    /// Whether to require us to be anchored.
    /// Here because HTN does not allow us to continuously check a condition by itself.
    /// Ignored if we're not anchorable.
    /// </summary>
    [DataField]
    public bool RequireAnchored = true;

    /// <summary>
    /// Stop targeting if beyond this range.
    /// </summary>
    [DataField]
    public float MaxTargetingRange = 2000f;

    private const string TargetingCancelToken = "ShipTargetingCancelToken";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _targeting = sysManager.GetEntitySystem<ShipTargetingSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityCoordinates>(TargetKey, out var targetCoordinates, _entManager))
        {
            return (false, null);
        }

        return (true, new Dictionary<string, object>()
        {
            {NPCBlackboard.OwnerCoordinates, targetCoordinates}
        });
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        // Need to remove the planning value for execution.
        blackboard.Remove<EntityCoordinates>(NPCBlackboard.OwnerCoordinates);
        var targetCoordinates = blackboard.GetValue<EntityCoordinates>(TargetKey);
        var uid = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        var comp = _targeting.Target(uid, targetCoordinates);

        if (comp == null)
            return;

        comp.LeadingAccuracy = LeadingAccuracy;
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityCoordinates>(TargetKey, out var target, _entManager)
            || !_entManager.TryGetComponent<TransformComponent>(owner, out var xform)
            // also fail if we're anchorable but are unanchored and require to be anchored
            || _entManager.TryGetComponent<AnchorableComponent>(owner, out var anchorable)
                && !xform.Anchored && RequireAnchored
        )
            return HTNOperatorStatus.Failed;

        // ensure we're still targeting if we e.g. move grids
        var comp = _targeting.Target(owner, target, false);
        if (comp == null)
            return HTNOperatorStatus.Finished;

        if (target.EntityId == EntityUid.Invalid || !xform.Coordinates.TryDistance(_entManager, target, out var distance) || distance > MaxTargetingRange)
            return HTNOperatorStatus.Finished;

        if (ShutdownState == HTNPlanState.PlanFinished)
        {
            return HTNOperatorStatus.Finished;
        }

        return HTNOperatorStatus.Continuing;
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        // Cleanup the blackboard and remove steering.
        if (blackboard.TryGetValue<CancellationTokenSource>(TargetingCancelToken, out var cancelToken, _entManager))
        {
            cancelToken.Cancel();
            blackboard.Remove<CancellationTokenSource>(TargetingCancelToken);
        }

        if (RemoveKeyOnFinish)
            blackboard.Remove<EntityCoordinates>(TargetKey);

        _targeting.Stop(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner));
    }
}
