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

public sealed partial class CEZGroundFrictionController : VirtualController
{
    [Dependency] private CEZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
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

            var grip = _zLevels.GetGroundGrip(uid);
            if (grip <= 0f)
                continue;

            ApplyLinearScrape(uid, body, grip, frameTime);
            ApplyAngularScrape(uid, body, grip, frameTime);
        }
    }

    private void ApplyLinearScrape(EntityUid uid, PhysicsComponent body, float grip, float frameTime)
    {
        var predicted = body.LinearVelocity + body.Force * body.InvMass * frameTime;
        var speed = predicted.Length();

        if (speed <= 0f)
            return;

        var drop = MathF.Min(CEZLevelsSystem.GroundSkidDecel * grip * frameTime, speed);

        var force = predicted / speed * -drop / (body.InvMass * frameTime);
        PhysicsSystem.ApplyForce(uid, force, body: body);
    }

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
