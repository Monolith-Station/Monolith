/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._CE.ZLevels.Pulling;

public sealed partial class CEZLevelPullingSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CESharedZLevelsSystem _levels = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float TransitionSpeed = 5f;
    private readonly Dictionary<EntityUid, Transition> _pending = new();
    private readonly List<EntityUid> _processing = new();

    private readonly record struct Transition(EntityUid Puller, MapCoordinates Entry, MapId Destination, int Offset, TimeSpan Deadline);

    public override void Initialize()
    {
        SubscribeLocalEvent<ActivePullerComponent, CEZLevelBeforeMapMoveEvent>(OnPullerMove);
    }

    private void OnPullerMove(Entity<ActivePullerComponent> puller, ref CEZLevelBeforeMapMoveEvent args)
    {
        if (args.Destination is not { } destination ||
            CompOrNull<PullerComponent>(puller)?.Pulling is not { } target ||
            !TryComp<TransformComponent>(target, out var targetXform))
            return;

        var entry = _transform.GetMapCoordinates(puller);
        if (targetXform.MapID != entry.MapId || !_interaction.InRangeUnobstructed(puller.Owner, target))
            return;

        _pending[target] = new Transition(puller, entry, destination, args.Offset, _timing.CurTime + TimeSpan.FromSeconds(1));
    }

    public override void Update(float frameTime)
    {
        _processing.AddRange(_pending.Keys);
        foreach (var target in _processing)
        {
            if (!Advance(target, _pending[target], frameTime))
                _pending.Remove(target);
        }
        _processing.Clear();
    }

    private bool Advance(EntityUid target, Transition transition, float frameTime)
    {
        var puller = transition.Puller;
        if (TerminatingOrDeleted(puller) || TerminatingOrDeleted(target) ||
            !TryComp<PullerComponent>(puller, out var pullerComp) ||
            !TryComp<PullableComponent>(target, out var pullable) ||
            pullerComp.Pulling is { } otherTarget && otherTarget != target ||
            pullable.Puller is { } otherPuller && otherPuller != puller ||
            !TryComp<PhysicsComponent>(target, out var body) || body.BodyType == BodyType.Static ||
            HasComp<MapGridComponent>(target) || !_pulling.CanPull(puller, target, pullerComp))
            return false;

        var xform = Transform(target);
        if (xform.Anchored || (xform.ParentUid != xform.GridUid && xform.ParentUid != xform.MapUid) ||
            Transform(puller).MapID != transition.Destination || _timing.CurTime > transition.Deadline)
            return false;

        var exit = new MapCoordinates(transition.Entry.Position, transition.Destination);
        if (!_interaction.InRangeUnobstructed(exit, puller, predicate: uid => uid == target))
            return false;

        if (xform.MapID == transition.Entry.MapId)
        {
            if (!_interaction.InRangeUnobstructed(transition.Entry, target))
                return false;

            var position = _transform.GetWorldPosition(xform);
            var delta = transition.Entry.Position - position;
            var step = frameTime * TransitionSpeed;
            var distance = delta.Length();
            if (distance > step)
            {
                _transform.SetWorldPosition(target, position + delta * (step / distance));
                return true;
            }

            _transform.SetWorldPosition(target, transition.Entry.Position);
            // Walking off a transit grid can already have handed the target to the real stack.
            if (xform.MapID == transition.Entry.MapId)
                _levels.TryMoveToZDestination(target, transition.Offset, transition.Destination);
        }

        if (xform.MapID == transition.Destination && _interaction.InRangeUnobstructed(puller, target))
            _pulling.TryStartPull(puller, target, pullerComp, pullable);

        return false;
    }
}
