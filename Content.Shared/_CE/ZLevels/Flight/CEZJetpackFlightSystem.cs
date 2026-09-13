/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Movement.Components;

namespace Content.Shared._CE.ZLevels.Flight;

/// <summary>
/// Lets you move up and down with a jetpack.
/// </summary>
public sealed partial class CEZJetpackFlightSystem : EntitySystem
{
    /// <summary>
    /// Settle zone around each level.
    /// </summary>
    private const float SettleZone = 0.25f;

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
        // No Z levels to move through.
        if (!TryComp<CEZPhysicsComponent>(ent, out var zPhys))
            return;

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

        // Force gravity update.
        _zLevels.UpdateGravityState((ent, zPhys));
    }

    private void OnGetGravity(Entity<JetpackUserComponent> ent, ref CECheckGravityEvent args)
    {
        if (ent.Comp.LifeStage >= ComponentLifeStage.Stopping)
            return;

        // No Z-gravity with your jetpack on.
        args.Gravity *= 0f;
    }

    private void OnGetZVelocity(Entity<JetpackUserComponent> ent, ref CEGetZVelocityEvent args)
    {
        if (!TryComp<JetpackComponent>(ent.Comp.Jetpack, out var jetpack))
            return; // If this somehow fails, god help us all

        jetpack.IsZMoving = false;

        var pos = args.Target.Comp.LocalPosition;
        var velocity = args.Target.Comp.Velocity;
        var input = (ent.Comp.AscendHeld ? 1f : 0f) - (ent.Comp.DescendHeld ? 1f : 0f);

        float target;
        if (input != 0f)
            target = input * jetpack.FlightMaxSpeed;
        else if (pos <= SettleZone)
            target = -pos * jetpack.FlightSettleGain;
        else if (pos >= 1f - SettleZone)
            target = (1f - pos) * jetpack.FlightSettleGain;
        else
            target = 0f;

        // Lerp towards target.
        var newVelocity = velocity + (target - velocity) * jetpack.FlightResponsiveness * args.FrameTime;

        if (input == 0f && pos <= SettleZone && pos + newVelocity * args.FrameTime <= 0f)
        {
            if (pos != 0f)
                _zLevels.SetZPosition(args.Target.AsNullable(), 0f);
            if (velocity != 0f)
                _zLevels.SetZVelocity(args.Target.AsNullable(), 0f);
            return;
        }

        if (MathF.Abs(newVelocity - velocity) > 0.001f)
        {
            jetpack.IsZMoving = true;
            _zLevels.SetZVelocity(args.Target.AsNullable(), newVelocity);
        }
    }
}
