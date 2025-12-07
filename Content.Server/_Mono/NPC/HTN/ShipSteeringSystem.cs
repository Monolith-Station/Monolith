using System.Numerics;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.Physics.Controllers;
using Content.Server.Shuttles.Components;
using Content.Shared.CCVar;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.NPC.Events;
using Content.Shared.Physics;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mono.NPC.HTN;

public sealed partial class ShipSteeringSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly MoverController _mover = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipSteererComponent, GetShuttleInputsEvent>(OnSteererGetInputs);
        SubscribeLocalEvent<ShipSteererComponent, PilotedShuttleRelayedEvent<StartCollideEvent>>(OnShuttleStartCollide);

        Subs.CVar(_cfg, CCVars.NPCEnabled, enabled => _enabled = enabled, true);
    }

    // have to use this because RT's is broken and unusable for navigation
    // another algorithm stolen from myself from orbitfight
    public Angle ShortestAngleDistance(Angle from, Angle to)
    {
        var diff = (to - from) % Math.Tau;
        return diff + Math.Tau * (diff < -Math.PI ? 1 : diff > Math.PI ? -1 : 0);
    }

    private void OnSteererGetInputs(Entity<ShipSteererComponent> ent, ref GetShuttleInputsEvent args)
    {
        var pilotXform = Transform(ent);

        var shipUid = pilotXform.ParentUid;
        var shipXform = Transform(shipUid);
        if (ent.Comp.Status == ShipSteeringStatus.InRange
            || !TryComp<ShuttleComponent>(shipUid, out var shuttle)
            || !TryComp<PhysicsComponent>(shipUid, out var shipBody))
        {
            ent.Comp.Status = ShipSteeringStatus.InRange;
            return;
        }

        args.GotInput = true;

        var target = ent.Comp.Coordinates;
        var targetUid = target.EntityId; // if we have a target try to lead it
        var mapTarget = _transform.ToMapCoordinates(target);

        var shipPos = _transform.GetMapCoordinates(shipXform);
        var shipNorthAngle = _transform.GetWorldRotation(shipUid);

        if (mapTarget.MapId != shipPos.MapId)
            return;

        var toTargetVec = mapTarget.Position - shipPos.Position;
        var distance = toTargetVec.Length();

        var needBrake = ent.Comp.InRangeMaxSpeed != null;
        var maxArrivedVel = ent.Comp.InRangeMaxSpeed ?? 0.1f;
        var angVel = shipBody.AngularVelocity;

        var linVel = shipBody.LinearVelocity;

        var minDistance = ent.Comp.RangeTolerance == null ? 0f : ent.Comp.Range - ent.Comp.RangeTolerance.Value;

        if (distance <= ent.Comp.Range && distance >= minDistance)
        {
            if (!needBrake)
            {
                ent.Comp.Status = ShipSteeringStatus.InRange;
                return;
            }

            if (linVel.Length() <= maxArrivedVel && angVel < ent.Comp.MaxRotateRate)
                ent.Comp.Status = ShipSteeringStatus.InRange;
            else
                args.Input = new ShuttleInput(Vector2.Zero, 0f, 1f);
        }

        ent.Comp.Status = ShipSteeringStatus.Moving;

        var isInside = false;
        // if we're too close, go to min radius
        // transform target point to closest point on inner wanted circle, if we have one and we're inside
        if (distance < minDistance)
        {
            isInside = true;
            mapTarget = mapTarget.Offset((-toTargetVec).Normalized() * minDistance);
            toTargetVec = mapTarget.Position - shipPos.Position;
            distance = toTargetVec.Length();
        }

        var strafeInput = Vector2.Zero;

        // now calculate our braking path
        var brakeInput = 0f;
        var brakeVec = GetGoodThrustVector((-shipNorthAngle).RotateVec(-linVel), shuttle);
        var brakeThrust = _mover.GetDirectionThrust(brakeVec, shuttle, shipBody) * ShuttleComponent.BrakeCoefficient;
        var brakeAccel = brakeThrust * shipBody.InvMass;
        var brakePath = linVel.Length() > 0 ? linVel.LengthSquared() / (2f * brakeAccel.Length()) : 0f;

        if (brakePath + (isInside ? -ent.Comp.RangeTolerance : ent.Comp.Range) > distance && needBrake)
        {
            brakeInput = 1f;
        }
        else
        {
            var linVelDir = Vector2.Zero;
            // lead target if we don't want to brake
            if (!needBrake && ent.Comp.LeadingEnabled && TryComp<PhysicsComponent>(targetUid, out var targetBody))
            {
                var deltaVel = linVel - targetBody.LinearVelocity;
                linVelDir = deltaVel.Length() == 0 ? Vector2.Zero : deltaVel.Normalized();
            }
            else if (linVel.Length() != 0)
            {
                linVelDir = linVel.Normalized();
            }
            var toTargetDir = toTargetVec.Normalized();
            // mirror linVelDir in relation to toTargetDir
            // for that we orthogonalize it then invert it to get the perpendicular-vector
            var adjustDir = -(linVelDir - toTargetDir * Vector2.Dot(linVelDir, toTargetDir));
            var globalStrafeInput = toTargetDir + adjustDir * 2;
            strafeInput = (-shipNorthAngle).RotateVec(globalStrafeInput);
            strafeInput = GetGoodThrustVector(strafeInput, shuttle);
        }

        var targetAngle = toTargetVec.ToWorldAngle();
        if (strafeInput.Length() > 0 && ent.Comp.LeadingEnabled)
            targetAngle = shipNorthAngle + strafeInput.ToWorldAngle();

        var angAccel = _mover.GetAngularAcceleration(shuttle, shipBody);
        var brakeAngleDelta = angAccel == 0f ? 0f : (angVel * angVel) / (2f * angAccel);
        brakeAngleDelta *= Math.Sign(angVel);
        // there's 500 different standards on how to count angles so needs the +PI
        var wishRotateBy = new Angle(ent.Comp.TargetRotation) + ShortestAngleDistance(shipNorthAngle + new Angle(Math.PI), targetAngle);
        var rotateDelta = ShortestAngleDistance(new Angle(brakeAngleDelta), wishRotateBy);
        var rotationInput = -(float)rotateDelta.Theta;
        rotationInput = MathF.Sign(rotationInput);
        // don't overbrake if we're braking
        if (angVel * rotationInput < 0)
            rotationInput *= MathF.Min(1f, MathF.Abs(angVel) / (angAccel * args.FrameTime));

        args.Input = new ShuttleInput(strafeInput, rotationInput, brakeInput);
    }

    private void OnShuttleStartCollide(Entity<ShipSteererComponent> ent, ref PilotedShuttleRelayedEvent<StartCollideEvent> outerArgs) {
        var args = outerArgs.Args;

        // finish movement if we collided with target and want to finish in this case
        if (ent.Comp.FinishOnCollide && args.OtherEntity == ent.Comp.Coordinates.EntityId)
            ent.Comp.Status = ShipSteeringStatus.InRange;
    }

    /// <summary>
    /// Checks if thrust in any direction this vector wants to go to is blocked, and zeroes it out in that direction if necessary.
    /// </summary>
    public Vector2 GetGoodThrustVector(Vector2 wish, ShuttleComponent shuttle, float threshold = 0.125f, float lenThreshold = 2f)
    {
        var res = wish;

        var horizIndex = wish.X > 0 ? 1 : 3; // east else west
        var vertIndex = wish.Y > 0 ? 2 : 0; // north else south
        var horizThrust = shuttle.LinearThrust[horizIndex];
        var vertThrust = shuttle.LinearThrust[vertIndex];

        var normWish = wish.Normalized();
        var wishX = MathF.Abs(normWish.X);
        var wishY = MathF.Abs(normWish.Y);

        if (horizThrust * wishX < vertThrust * threshold * wishY)
            res.X = 0f;
        if (vertThrust * wishY < horizThrust * threshold * wishX)
            res.Y = 0f;

        return res.Normalized();
    }

    /// <summary>
    /// Adds the AI to the steering system to move towards a specific target
    /// </summary>
    public ShipSteererComponent Steer(EntityUid uid, EntityCoordinates coordinates, ShipSteererComponent? component = null)
    {
        var xform = Transform(uid);
        var shipUid = xform.ParentUid;
        if (TryComp<ShuttleComponent>(shipUid, out var shuttle))
            _mover.AddPilot(shipUid, uid);

        if (!Resolve(uid, ref component, false))
            component = AddComp<ShipSteererComponent>(uid);

        component.Coordinates = coordinates;

        return component;
    }

    /// <summary>
    /// Stops the steering behavior for the AI and cleans up.
    /// </summary>
    public void Stop(EntityUid uid, ShipSteererComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        RemComp<ShipSteererComponent>(uid);
    }
}
