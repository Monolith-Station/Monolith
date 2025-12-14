using Content.Server._Mono.NPC.HTN;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Systems;
using Content.Shared.CCVar;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server._Mono.NPC.HTN.Operators;

/// <summary>
/// Moves parent shuttle to specified target key. Hands the actual steering off to ShipSteeringSystem.
/// </summary>
public sealed partial class ShipMoveToOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private SharedTransformSystem _transform = default!;
    private ShipSteeringSystem _steering = default!;

    /// <summary>
    /// When to shut the task down.
    /// </summary>
    [DataField]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    /// <summary>
    /// Whether to trust away from obstacles.
    /// </summary>
    [DataField]
    public bool AvoidCollisions = true;

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
    /// How close we need to get before considering movement finished.
    /// </summary>
    [DataField]
    public float Range = 5f;

    /// <summary>
    /// Velocity below which we count as successfully braked.
    /// Don't care about velocity if null.
    /// </summary>
    [DataField]
    public float? BrakeMaxVelocity = 0.1f;

    /// <summary>
    /// Rotation to move at relative to direction to target.
    /// </summary>
    [DataField]
    public float TargetRotation = 0f;

    private const string MovementCancelToken = "ShipMovementCancelToken";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
        _steering = sysManager.GetEntitySystem<ShipSteeringSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityCoordinates>(TargetKey, out var targetCoordinates, _entManager))
        {
            return (false, null);
        }

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<TransformComponent>(owner, out var xform) ||
            !_entManager.TryGetComponent<PhysicsComponent>(owner, out var body))
            return (false, null);

        if (xform.Coordinates.TryDistance(_entManager, targetCoordinates, out var distance)
            && distance <= Range)
        {
            // In range
            return (true, new Dictionary<string, object>()
            {
                {NPCBlackboard.OwnerCoordinates, blackboard.GetValueOrDefault<EntityCoordinates>(NPCBlackboard.OwnerCoordinates, _entManager)}
            });
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

        // Re-use the path we may have if applicable.
        var comp = _steering.Steer(uid, targetCoordinates);

        comp.Range = Range;
        comp.InRangeMaxSpeed = BrakeMaxVelocity;
        comp.AvoidCollisions = AvoidCollisions;
        comp.TargetRotation = TargetRotation;
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<ShipSteererComponent>(owner, out var steerer))
            return HTNOperatorStatus.Failed;

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
