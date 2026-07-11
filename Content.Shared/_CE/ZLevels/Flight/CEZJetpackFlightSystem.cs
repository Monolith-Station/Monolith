/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Movement.Components;

namespace Content.Shared._CE.ZLevels.Flight;

/// <summary>
/// Bridges jetpacks into z-level physics: while a jetpack is active (its wearer carries a
/// <see cref="JetpackUserComponent"/>) the wearer's z-gravity is cancelled so it floats, and
/// the shuttle ascend/descend input on that component steers it up and down the levels. All of
/// it keys straight off the active-jetpack marker — no bespoke flight component — so it simply
/// applies whenever a jetpack is on, planet gravity included. Shared so the float and vertical
/// thrust are predicted on the client, matching how the rest of z-physics runs.
/// </summary>
public sealed partial class CEZJetpackFlightSystem : EntitySystem
{
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JetpackUserComponent, ComponentStartup>(OnJetpackStart);
        SubscribeLocalEvent<JetpackUserComponent, ComponentShutdown>(OnJetpackStop);

        SubscribeLocalEvent<JetpackUserComponent, CECheckGravityEvent>(OnGetGravity);
        SubscribeLocalEvent<JetpackUserComponent, CEGetZVelocityEvent>(OnGetZVelocity);
    }

    private void OnJetpackStart(Entity<JetpackUserComponent> ent, ref ComponentStartup args)
    {
        // Off the z-levels (open space), the jetpack just works laterally as normal.
        if (!TryComp<CEZPhysicsComponent>(ent, out var zPhys))
            return;

        // Feed the per-substep velocity event so ascend/descend can steer, and recompute gravity
        // now that the float hook below is attached.
        ent.Comp.PriorVelocityRaiseEvent = zPhys.VelocityRaiseEvent;
        zPhys.VelocityRaiseEvent = true;

        _zLevels.UpdateGravityState((ent, zPhys));
        _zLevels.WakeBody((ent, zPhys));
    }

    private void OnJetpackStop(Entity<JetpackUserComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<CEZPhysicsComponent>(ent, out var zPhys))
            return;

        zPhys.VelocityRaiseEvent = ent.Comp.PriorVelocityRaiseEvent;

        // Recompute gravity so weight returns. The gravity handler above bails while this
        // component is Stopping, so this recompute no longer includes the jetpack's cancel.
        _zLevels.UpdateGravityState((ent, zPhys));
    }

    private void OnGetGravity(Entity<JetpackUserComponent> ent, ref CECheckGravityEvent args)
    {
        // Skip once the component is stopping: OnJetpackStop recomputes gravity DURING this
        // component's own shutdown (it still exists and still receives this directed event then),
        // so contributing here would re-zero gravity as the jetpack turns off and leave the
        // wearer weightless forever. Existence alone isn't "active" — Stopping means we're done.
        if (ent.Comp.LifeStage >= ComponentLifeStage.Stopping)
            return;

        // Cancel z-gravity so the wearer hovers instead of dropping to the deck.
        args.Gravity *= 0f;
    }

    private void OnGetZVelocity(Entity<JetpackUserComponent> ent, ref CEGetZVelocityEvent args)
    {
        var input = (ent.Comp.AscendHeld ? 1f : 0f) - (ent.Comp.DescendHeld ? 1f : 0f);

        var maxSpeed = 3f;
        var responsiveness = 8f;
        var settleGain = 1f;
        if (TryComp<JetpackComponent>(ent.Comp.Jetpack, out var jetpack))
        {
            maxSpeed = jetpack.FlightMaxSpeed;
            responsiveness = jetpack.FlightResponsiveness;
            settleGain = jetpack.FlightSettleGain;
        }

        float target;
        if (input != 0f)
        {
            // Actively steering: chase the held direction at full speed.
            target = input * maxSpeed;
        }
        else
        {
            // Idle: gradual pull toward the nearest level plane (a normalized altitude), like a
            // transit set settling onto a level — down to the floor from the lower half, up into
            // the level above from the upper half. LocalPosition is 0 at the floor, 1 at the next
            // level. The pull is proportional to the distance left, so it eases to zero right at
            // the plane instead of overshooting and oscillating across the boundary (which the
            // nearest-plane-with-min-speed transit form would do here, since a walker never
            // "exits" a level the way a transit set does). Crossing 1 hands off to the level
            // above at LocalPosition ~0, where the pull is already spent — so it comes to rest.
            var pos = args.Target.Comp.LocalPosition;
            target = pos <= 0.5f
                ? -pos * settleGain               // drift down to this level's floor
                : (1f - pos) * settleGain;        // drift up into the level above
        }

        args.VelocityDelta += (target - args.Target.Comp.Velocity) * responsiveness;
    }
}
