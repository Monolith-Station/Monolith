/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Movement.Components;

namespace Content.Server._CE.ZLevels.Flight;

/// <summary>
/// Bridges jetpacks into z-level physics: while a jetpack is active (its wearer carries a
/// <see cref="JetpackUserComponent"/>) the wearer's z-gravity is cancelled so it floats, and
/// the shuttle ascend/descend input on that component steers it up and down the levels. All of
/// it keys straight off the active-jetpack marker — no bespoke flight component — so it simply
/// applies whenever a jetpack is on, planet gravity included.
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

        // Weight returns now that the float hook is gone (the component is mid-shutdown, so its
        // gravity handler no longer contributes).
        _zLevels.UpdateGravityState((ent, zPhys));
    }

    private void OnGetGravity(Entity<JetpackUserComponent> ent, ref CECheckGravityEvent args)
    {
        // Cancel z-gravity so the wearer hovers instead of dropping to the deck.
        args.Gravity *= 0f;
    }

    private void OnGetZVelocity(Entity<JetpackUserComponent> ent, ref CEGetZVelocityEvent args)
    {
        var input = (ent.Comp.AscendHeld ? 1f : 0f) - (ent.Comp.DescendHeld ? 1f : 0f);

        var maxSpeed = 3f;
        var responsiveness = 8f;
        if (TryComp<JetpackComponent>(ent.Comp.Jetpack, out var jetpack))
        {
            maxSpeed = jetpack.FlightMaxSpeed;
            responsiveness = jetpack.FlightResponsiveness;
        }

        // Ease vertical velocity toward the input target; released, the target is 0, so the
        // wearer coasts to a hover (gravity is already cancelled, so it holds there).
        var target = input * maxSpeed;
        args.VelocityDelta += (target - args.Target.Comp.Velocity) * responsiveness;
    }
}
