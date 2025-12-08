using System.Numerics;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.Physics.Controllers;
using Content.Server.Shuttles.Components;
using Content.Shared.CCVar;
using Content.Shared.Construction.Components;
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

        var shipUid = pilotXform.GridUid;

        /// 1. check if we can drive at all
        if (ent.Comp.Status == ShipSteeringStatus.InRange
            || shipUid == null
            || !pilotXform.Anchored && ent.Comp.RequireAnchored && HasComp<AnchorableComponent>(ent)
            || !TryComp<ShuttleComponent>(shipUid, out var shuttle)
            || !TryComp<PhysicsComponent>(shipUid, out var shipBody))
        {
            ent.Comp.Status = ShipSteeringStatus.InRange;
            return;
        }

        var shipXform = Transform(shipUid.Value);
        args.GotInput = true;

        var target = ent.Comp.Coordinates;
        var targetUid = target.EntityId; // if we have a target try to lead it
        var mapTarget = _transform.ToMapCoordinates(target);

        var shipPos = _transform.GetMapCoordinates(shipXform);
        var shipNorthAngle = _transform.GetWorldRotation(shipUid.Value);

        // we or target might just be in FTL so don't count us as finished
        if (mapTarget.MapId != shipPos.MapId)
            return;

        var toTargetVec = mapTarget.Position - shipPos.Position;
        var distance = toTargetVec.Length();

        var angVel = shipBody.AngularVelocity;
        var linVel = shipBody.LinearVelocity;

        var maxArrivedVel = ent.Comp.InRangeMaxSpeed ?? float.PositiveInfinity;
        var maxArrivedAngVel = ent.Comp.MaxRotateRate ?? float.PositiveInfinity;
        var minDistance = (ent.Comp.Range - ent.Comp.RangeTolerance) ?? 0f;

        var targetAngleOffset = new Angle(ent.Comp.TargetRotation);

        var highRange = ent.Comp.Range + (ent.Comp.RangeTolerance ?? 0f);
        var lowRange = (ent.Comp.Range - ent.Comp.RangeTolerance) ?? 0f;
        var midRange = (highRange + lowRange) / 2f;

        // check if all good
        if (distance >= lowRange && distance <= highRange
            && linVel.Length() < maxArrivedVel
            && MathF.Abs(angVel) < maxArrivedAngVel)
        {
            ent.Comp.Status = ShipSteeringStatus.InRange;
            return;
        }

        /// 2. process where we want to move
        // get our actual move target, which will be a point at a circle of the radius we wish to be at
        var destMapPos = mapTarget.Offset(NormalizedOrZero(-toTargetVec) * midRange);
        var toDestVec = destMapPos.Position - shipPos.Position;
        var destDistance = toDestVec.Length();

        /// 3. handle braking and movement
        var brakeVec = GetGoodThrustVector((-shipNorthAngle).RotateVec(-linVel), shuttle);
        var brakeThrust = _mover.GetDirectionThrust(brakeVec, shuttle, shipBody) * ShuttleComponent.BrakeCoefficient;
        var brakeAccelVec = brakeThrust * shipBody.InvMass;
        var brakeAccel = brakeAccelVec.Length();
        // check what's our brake path until we hit our desired minimum velocity
        var brakePath = linVel.LengthSquared() / (2f * brakeAccel);
        var innerBrakePath = maxArrivedVel / (2f * brakeAccel);
        // negative if we're already slow enough
        var leftoverBrakePath = brakeAccel == 0f ? 0f : brakePath - innerBrakePath;

        var effectiveVel = linVel;
        if (ent.Comp.LeadingEnabled && TryComp<PhysicsComponent>(targetUid, out var targetBody))
            effectiveVel -= targetBody.LinearVelocity;

        var wishInputVec = Vector2.Zero;
        if (leftoverBrakePath > destDistance)
        {
            wishInputVec = -effectiveVel;
        }
        else
        {
            var linVelDir = NormalizedOrZero(effectiveVel);
            var toDestDir = NormalizedOrZero(toDestVec);
            // mirror linVelDir in relation to toTargetDir
            // for that we orthogonalize it then invert it to get the perpendicular-vector
            var adjustDir = -(linVelDir - toDestDir * Vector2.Dot(linVelDir, toDestDir));
            wishInputVec = toDestDir + adjustDir * 2;
        }

        var strafeInput = Vector2.Zero;
        var brakeInput = 0f;
        if (Vector2.Dot(wishInputVec, -linVel) >= ent.Comp.BrakeThreshold)
        {
            brakeInput = 1f;
        }
        else
        {
            strafeInput = (-shipNorthAngle).RotateVec(wishInputVec);
            strafeInput = GetGoodThrustVector(strafeInput, shuttle);
        }

        /// 5. handle rotation
        var wishAngle = new Angle(0);
        if (ent.Comp.AlwaysFaceTarget)
            wishAngle = toTargetVec.ToWorldAngle();
        // try to face our thrust direction if we can
        // TODO: determine best thrust direction and face accordingly
        else if (strafeInput.Length() > 0)
            wishAngle = wishInputVec.ToWorldAngle();
        else
            wishAngle = toDestVec.ToWorldAngle();

        var angAccel = _mover.GetAngularAcceleration(shuttle, shipBody);
        var brakeAngleDelta = angAccel == 0f ? 0f : (angVel * angVel) / (2f * angAccel);
        brakeAngleDelta *= Math.Sign(angVel);
        // there's 500 different standards on how to count angles so needs the +PI
        var wishRotateBy = targetAngleOffset + ShortestAngleDistance(shipNorthAngle + new Angle(Math.PI), wishAngle);
        var rotateDelta = ShortestAngleDistance(new Angle(brakeAngleDelta), wishRotateBy);
        var rotationInput = -(float)rotateDelta.Theta;
        rotationInput = MathF.Sign(rotationInput);
        // don't overbrake if we're braking
        if (angVel * rotationInput < 0)
            rotationInput *= MathF.Min(1f, MathF.Abs(angVel) / (angAccel * args.FrameTime));

        /// 6. output
        args.Input = new ShuttleInput(strafeInput, rotationInput, brakeInput);
    }

    private void OnShuttleStartCollide(Entity<ShipSteererComponent> ent, ref PilotedShuttleRelayedEvent<StartCollideEvent> outerArgs) {
        var args = outerArgs.Args;

        // finish movement if we collided with target and want to finish in this case
        if (ent.Comp.FinishOnCollide && args.OtherEntity == ent.Comp.Coordinates.EntityId)
            ent.Comp.Status = ShipSteeringStatus.InRange;
    }

    public Vector2 NormalizedOrZero(Vector2 vec)
    {
        return vec.LengthSquared() == 0 ? Vector2.Zero : vec.Normalized();
    }

    /// <summary>
    /// Checks if thrust in any direction this vector wants to go to is blocked, and zeroes it out in that direction if necessary.
    /// </summary>
    public Vector2 GetGoodThrustVector(Vector2 wish, ShuttleComponent shuttle, float threshold = 0.125f, float lenThreshold = 2f)
    {
        var res = wish;
        res.Normalize();

        var horizIndex = wish.X > 0 ? 1 : 3; // east else west
        var vertIndex = wish.Y > 0 ? 2 : 0; // north else south
        var horizThrust = shuttle.LinearThrust[horizIndex];
        var vertThrust = shuttle.LinearThrust[vertIndex];

        var wishX = MathF.Abs(res.X);
        var wishY = MathF.Abs(res.Y);

        if (horizThrust * wishX < vertThrust * threshold * wishY)
            res.X = 0f;
        if (vertThrust * wishY < horizThrust * threshold * wishX)
            res.Y = 0f;

        return res;
    }

    /// <summary>
    /// Adds the AI to the steering system to move towards a specific target.
    /// Returns null on failure.
    /// </summary>
    public ShipSteererComponent? Steer(EntityUid uid, EntityCoordinates coordinates, ShipSteererComponent? component = null)
    {
        var xform = Transform(uid);
        var shipUid = xform.GridUid;
        if (TryComp<ShuttleComponent>(shipUid, out var shuttle))
            _mover.AddPilot(shipUid.Value, uid);
        else
            return null;

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
