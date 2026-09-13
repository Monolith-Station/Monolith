/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Server.Physics.Controllers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;

namespace Content.Server._CE.ZLevels.Core;

/// <summary>
/// Applies the constant (Coulomb) half of the ground scrape as a real force, inside the physics
/// step, so it actually competes with thrust.
/// </summary>
public sealed partial class CEZGroundFrictionController : VirtualController
{
    [Dependency] private CEZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        // MUST be registered before base.Initialize(), which snapshots UpdatesBefore/UpdatesAfter
        // into arrays and subscribes with them — anything added afterwards is silently ignored and
        // the controller ends up unordered.
        //
        // The ordering is not a nicety. MoverController applies shuttle thrust in this same
        // UpdateBeforeSolve phase, and the clamp below reads that thrust out of
        // PhysicsComponent.Force to work out how much of the coming velocity it may cancel. Run
        // first and the accumulator is still empty, the predicted velocity is just the current one,
        // and a stationary hull gets no friction whatsoever — after which the thrust lands
        // unopposed and the ship creeps exactly as if none of this existed.
        UpdatesAfter.Add(typeof(MoverController));

        base.Initialize();
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        if (prediction)
            return;

        var query = EntityQueryEnumerator<CEZGridFallerComponent, MapGridComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var body))
        {
            if (body.InvMass <= 0f)
                continue;

            // Grip, not mere contact: a hull over a frictionless surface is on the ground and
            // simply slides, so there is nothing here to apply.
            var grip = _zLevels.GetGroundGrip(uid);
            if (grip <= 0f)
                continue;

            ApplyLinearScrape(uid, body, grip, frameTime);
            ApplyAngularScrape(uid, body, grip, frameTime);
        }
    }

    /// <summary>
    /// Opposes the velocity the body is about to have — current velocity plus whatever this step's
    /// accumulated force will add — rather than the velocity it has now.
    ///
    /// Predicting one step ahead is what makes the contact hold. Cancelling only present velocity
    /// leaves this step's thrust to land untouched, which is the creep; cancelling the predicted
    /// velocity means a hull whose engines cannot out-pull the scrape never gets moving in the first
    /// place. The cancellation is capped at exactly that predicted velocity, so the scrape can slow
    /// a hull to a dead stop but never shove it backwards.
    /// </summary>
    private void ApplyLinearScrape(EntityUid uid, PhysicsComponent body, float grip, float frameTime)
    {
        var predicted = body.LinearVelocity + body.Force * body.InvMass * frameTime;
        var speed = predicted.Length();

        if (speed <= 0f)
            return;

        var drop = MathF.Min(CEZLevelsSystem.GroundSkidDecel * grip * frameTime, speed);

        // Back out the force that produces exactly that change in velocity over this step.
        var force = predicted / speed * -drop / (body.InvMass * frameTime);
        PhysicsSystem.ApplyForce(uid, force, body: body);
    }

    /// <summary>
    /// The spin equivalent, against the angular velocity the body is about to have.
    /// </summary>
    private void ApplyAngularScrape(EntityUid uid, PhysicsComponent body, float grip, float frameTime)
    {
        if (body.InvI <= 0f)
            return;

        var predicted = body.AngularVelocity + body.Torque * body.InvI * frameTime;
        var spin = MathF.Abs(predicted);

        if (spin <= 0f)
            return;

        var drop = MathF.Min(CEZLevelsSystem.GroundSkidAngularDecel * grip * frameTime, spin);

        var torque = -MathF.Sign(predicted) * drop / (body.InvI * frameTime);
        PhysicsSystem.ApplyTorque(uid, torque, body: body);
    }
}
