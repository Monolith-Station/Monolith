using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Climbing.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared._CE.ZLevels.Climbing;

/// <summary>
/// Allows airborne entities to pass over climbable obstacles (fences, tables) without triggering a climb.
/// </summary>
public sealed class CEZLevelClimbingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEZPhysicsComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<CEZPhysicsComponent> ent, ref PreventCollideEvent args)
    {
        if (!TryComp<PhysicsComponent>(ent, out var physics) || physics.BodyStatus != BodyStatus.InAir)
            return;

        if (HasComp<ClimbableComponent>(args.OtherEntity))
            args.Cancelled = true;
    }
}
