using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Construction.Components;
using Robust.Shared.Map;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server._Mono.NPC.HTN.Operators;

/// <summary>
/// Moves parent shuttle to specified target key. Hands the actual steering off to ShipSteeringSystem.
/// </summary>
public sealed partial class ShipMoveToOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private ShipSteeringSystem _steering = default!;

    /// <summary>
    /// When to shut the task down.
    /// </summary>
    [DataField]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    /// <summary>
    /// When we're finished moving to the target should we remove its key?
    /// </summary>
    [DataField]
    public bool RemoveKeyOnFinish = true;

    /// <summary>
    /// Target Coordinates to move to. This gets removed after execution.
    /// </summary>
    [DataField]
    public string TargetKey = "ShipTargetCoordinates";

    /// <summary>
    /// Whether to keep facing target if backing off due to RangeTolerance.
    /// </summary>
    [DataField]
    public bool AlwaysFaceTarget = false;

    /// <summary>
    /// Whether to avoid obstacles.
    /// </summary>
    [DataField]
    public bool AvoidCollisions = true;

    /// <summary>
    /// How unwilling we are to use brake to adjust our velocity. Higher means less willing.
    /// </summary>
    [DataField]
    public float BrakeThreshold = 0.75f;

    /// <summary>
    /// Whether to consider the movement finished if we collide with target.
    /// </summary>
    [DataField]
    public bool FinishOnCollide = true;

    /// <summary>
    /// Velocity below which we count as successfully braked.
    /// Don't care about velocity if null.
    /// </summary>
    [DataField]
    public float? InRangeMaxSpeed = 0.1f;

    /// <summary>
    /// Whether to try to match velocity with target.
    /// </summary>
    [DataField]
    public bool LeadingEnabled = true;

    /// <summary>
    /// Max rotation rate to be considered stationary, if not null.
    /// </summary>
    [DataField]
    public float? MaxRotateRate = null;

    /// <summary>
    /// If target goes further than this, drop target.
    /// </summary>
    [DataField]
    public float MaxTargetingRange = 2000f;

    /// <summary>
    /// How close we need to get before considering movement finished.
    /// </summary>
    [DataField]
    public float Range = 5f;

    /// <summary>
    /// At most how far inside to have to stay into the desired range. If null, will consider the movement finished while in range.
    /// </summary>
    [DataField]
    public float? RangeTolerance = null;

    /// <summary>
    /// Whether to require us to be anchored.
    /// Here because HTN does not allow us to continuously check a condition by itself.
    /// Ignored if we're not anchorable.
    /// </summary>
    [DataField]
    public bool RequireAnchored = true;

    /// <summary>
    /// Rotation to move at relative to direction to target.
    /// </summary>
    [DataField]
    public float TargetRotation = 0f;

    private const string MovementCancelToken = "ShipMovementCancelToken";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _steering = sysManager.GetEntitySystem<ShipSteeringSystem>();
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

        var comp = _steering.Steer(uid, targetCoordinates);

        if (comp == null)
            return;

        comp.AlwaysFaceTarget = AlwaysFaceTarget;
        comp.AvoidCollisions = AvoidCollisions;
        comp.BrakeThreshold = BrakeThreshold;
        comp.FinishOnCollide = FinishOnCollide;
        comp.InRangeMaxSpeed = InRangeMaxSpeed;
        comp.LeadingEnabled = LeadingEnabled;
        comp.MaxRotateRate = MaxRotateRate;
        comp.Range = Range;
        comp.RangeTolerance = RangeTolerance;
        comp.TargetRotation = TargetRotation;
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<ShipSteererComponent>(owner, out var steerer)
            || !blackboard.TryGetValue<EntityCoordinates>(TargetKey, out var target, _entManager)
            || !_entManager.TryGetComponent<TransformComponent>(owner, out var xform)
            // also fail if we're anchorable but are unanchored and require to be anchored
            || _entManager.TryGetComponent<AnchorableComponent>(owner, out var anchorable)
                && !xform.Anchored && RequireAnchored
        )
            return HTNOperatorStatus.Failed;

        // ensure we're still steering if we e.g. move grids
        var comp = _steering.Steer(owner, target);
        if (comp == null)
            return HTNOperatorStatus.Failed;

        if (target.EntityId == EntityUid.Invalid || !xform.Coordinates.TryDistance(_entManager, target, out var distance) || distance > MaxTargetingRange)
            return HTNOperatorStatus.Finished;

        // Just keep moving in the background and let the other tasks handle it.
        if (ShutdownState == HTNPlanState.PlanFinished && steerer.Status == ShipSteeringStatus.Moving)
        {
            return HTNOperatorStatus.Finished;
        }

        return steerer.Status switch
        {
            ShipSteeringStatus.InRange => HTNOperatorStatus.Finished,
            ShipSteeringStatus.Moving => HTNOperatorStatus.Continuing,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        // Cleanup the blackboard and remove steering.
        if (blackboard.TryGetValue<CancellationTokenSource>(MovementCancelToken, out var cancelToken, _entManager))
        {
            cancelToken.Cancel();
            blackboard.Remove<CancellationTokenSource>(MovementCancelToken);
        }

        if (RemoveKeyOnFinish)
            blackboard.Remove<EntityCoordinates>(TargetKey);

        _steering.Stop(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner));
    }
}
