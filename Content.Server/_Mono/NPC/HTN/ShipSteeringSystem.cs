using System.Numerics;
using Content.Server.Physics.Controllers;
using Content.Server.Shuttles.Components;
using Content.Shared.Construction.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
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

    private EntityQuery<AnchorableComponent> _anchorableQuery;
    private EntityQuery<PhysicsComponent> _physQuery;
    private EntityQuery<ShuttleComponent> _shuttleQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipSteererComponent, GetShuttleInputsEvent>(OnSteererGetInputs);
        SubscribeLocalEvent<ShipSteererComponent, PilotedShuttleRelayedEvent<StartCollideEvent>>(OnShuttleStartCollide);

        _anchorableQuery = GetEntityQuery<AnchorableComponent>();
        _physQuery = GetEntityQuery<PhysicsComponent>();
        _shuttleQuery = GetEntityQuery<ShuttleComponent>();
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

        var target = ent.Comp.Coordinates;
        var targetUid = target.EntityId; // if we have a target try to lead it

        if (ent.Comp.Status == ShipSteeringStatus.InRange
            || shipUid == null
            || TerminatingOrDeleted(targetUid)
            || !pilotXform.Anchored && ent.Comp.RequireAnchored && _anchorableQuery.HasComp(ent)
            || !_shuttleQuery.TryComp(shipUid, out var shuttle)
            || !_physQuery.TryComp(shipUid, out var shipBody))
        {
            ent.Comp.Status = ShipSteeringStatus.InRange;
            return;
        }

        var shipXform = Transform(shipUid.Value);
        args.GotInput = true;

        var mapTarget = _transform.ToMapCoordinates(target);
        var shipPos = _transform.GetMapCoordinates(shipXform);

        // we or target might just be in FTL so don't count us as finished
        if (mapTarget.MapId != shipPos.MapId)
            return;

        var toTargetVec = mapTarget.Position - shipPos.Position;
        var distance = toTargetVec.Length();

        var angVel = shipBody.AngularVelocity;
        var linVel = shipBody.LinearVelocity;

        var maxArrivedVel = ent.Comp.InRangeMaxSpeed ?? float.PositiveInfinity;
        var maxArrivedAngVel = ent.Comp.MaxRotateRate ?? float.PositiveInfinity;

        var targetAngleOffset = new Angle(ent.Comp.TargetRotation);

        var highRange = ent.Comp.Range + (ent.Comp.RangeTolerance ?? 0f);
        var lowRange = (ent.Comp.Range - ent.Comp.RangeTolerance) ?? 0f;
        var midRange = (highRange + lowRange) / 2f;

        var targetVel = Vector2.Zero;
        if (ent.Comp.LeadingEnabled && _physQuery.TryComp(targetUid, out var targetBody))
            targetVel = targetBody.LinearVelocity;
        var relVel = linVel - targetVel;

        // check if all good
        if (distance >= lowRange && distance <= highRange
            && relVel.Length() < maxArrivedVel
            && MathF.Abs(angVel) < maxArrivedAngVel)
        {
            var good = true;
            if (ent.Comp.AlwaysFaceTarget)
            {
                var shipNorthAngle = _transform.GetWorldRotation(shipXform);
                var wishRotateBy = targetAngleOffset + ShortestAngleDistance(shipNorthAngle + new Angle(Math.PI), toTargetVec.ToWorldAngle());
                good = MathF.Abs((float)wishRotateBy.Theta) < ent.Comp.AlwaysFaceTargetOffset;
            }
            if (good)
            {
                ent.Comp.Status = ShipSteeringStatus.InRange;
                return;
            }
        }

        // get our actual move target, which will be either under us if we're in a position we're okay with, or a point in the middle of our target band
        var destMapPos = mapTarget;
        if (distance < lowRange || distance > highRange)
            destMapPos = destMapPos.Offset(NormalizedOrZero(-toTargetVec) * midRange);
        else
            destMapPos = shipPos;

        args.Input = ProcessMovement(shipXform, shipBody, shuttle,
                                     destMapPos, targetVel,
                                     maxArrivedVel, ent.Comp.BrakeThreshold, args.FrameTime,
                                     targetAngleOffset, ent.Comp.AlwaysFaceTarget ? toTargetVec.ToWorldAngle() : null);
    }

    private ShuttleInput ProcessMovement(TransformComponent shipXform, PhysicsComponent shipBody, ShuttleComponent shuttle,
                                         MapCoordinates destMapPos, Vector2 targetVel,
                                         float maxArrivedVel, float brakeThreshold, float frameTime,
                                         Angle targetAngleOffset, Angle? angleOverride)
    {

        var shipPos = _transform.GetMapCoordinates(shipXform);
        var shipNorthAngle = _transform.GetWorldRotation(shipXform);
        var angleVel = shipBody.AngularVelocity;
        var linVel = shipBody.LinearVelocity;

        var toDestVec = destMapPos.Position - shipPos.Position;
        var destDistance = toDestVec.Length();

        // try to lead the target with the target velocity we've been passed in
        var relVel = linVel - targetVel;

        var brakeVec = GetGoodThrustVector((-shipNorthAngle).RotateVec(-linVel), shuttle);
        var brakeThrust = _mover.GetDirectionThrust(brakeVec, shuttle, shipBody) * ShuttleComponent.BrakeCoefficient;
        var brakeAccelVec = brakeThrust * shipBody.InvMass;
        var brakeAccel = brakeAccelVec.Length();
        // check what's our brake path until we hit our desired minimum velocity
        var brakePath = linVel.LengthSquared() / (2f * brakeAccel);
        var innerBrakePath = maxArrivedVel / (2f * brakeAccel);
        // negative if we're already slow enough
        var leftoverBrakePath = brakeAccel == 0f ? 0f : brakePath - innerBrakePath;

        Vector2 wishInputVec;
        // if we can't brake then don't
        if (leftoverBrakePath > destDistance && brakeAccel != 0f)
        {
            wishInputVec = -relVel;
        }
        else
        {
            var linVelDir = NormalizedOrZero(relVel);
            var toDestDir = NormalizedOrZero(toDestVec);
            // mirror linVelDir in relation to toTargetDir
            // for that we orthogonalize it then invert it to get the perpendicular-vector
            var adjustDir = -(linVelDir - toDestDir * Vector2.Dot(linVelDir, toDestDir));
            wishInputVec = toDestDir + adjustDir * 2;
        }

        var strafeInput = (-shipNorthAngle).RotateVec(wishInputVec);
        strafeInput = GetGoodThrustVector(strafeInput, shuttle);


        Angle wishAngle;
        if (angleOverride != null)
            wishAngle = angleOverride.Value;
        // try to face our thrust direction if we can
        // TODO: determine best thrust direction and face accordingly
        else if (strafeInput.Length() > 0)
            wishAngle = wishInputVec.ToWorldAngle();
        else
            wishAngle = toDestVec.ToWorldAngle();

        var angAccel = _mover.GetAngularAcceleration(shuttle, shipBody);
        // there's 500 different standards on how to count angles so needs the +PI
        var wishRotateBy = targetAngleOffset + ShortestAngleDistance(shipNorthAngle + new Angle(Math.PI), wishAngle);
        var wishAngleVel = MathF.Sqrt(MathF.Abs((float)wishRotateBy) * 2f * angAccel) * Math.Sign(wishRotateBy);
        var wishDeltaAngleVel = wishAngleVel - angleVel;
        var rotationInput = angAccel == 0f ? 0f : -wishDeltaAngleVel / angAccel / frameTime;


        var brakeInput = 0f;
        // check if we should brake, brake if it's in a good direction and it won't stop us from rotating
        if (Vector2.Dot(NormalizedOrZero(wishInputVec), NormalizedOrZero(-linVel)) >= brakeThreshold
            && (MathF.Abs(rotationInput) < 1f - brakeThreshold || wishAngleVel * angleVel < 0 || MathF.Abs(wishAngleVel) < MathF.Abs(angleVel)))
        {
            brakeInput = 1f;
        }

        return new ShuttleInput(strafeInput, rotationInput, brakeInput);
    }

    private void OnShuttleStartCollide(Entity<ShipSteererComponent> ent, ref PilotedShuttleRelayedEvent<StartCollideEvent> outerArgs)
    {
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
    public Vector2 GetGoodThrustVector(Vector2 wish, ShuttleComponent shuttle, float threshold = 0.125f)
    {
        var res = NormalizedOrZero(wish);

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
    public ShipSteererComponent? Steer(Entity<ShipSteererComponent?> ent, EntityCoordinates coordinates)
    {
        var xform = Transform(ent);
        var shipUid = xform.GridUid;
        if (_shuttleQuery.TryComp(shipUid, out _))
            _mover.AddPilot(shipUid.Value, ent);
        else
            return null;

        if (!Resolve(ent, ref ent.Comp, false))
            ent.Comp = AddComp<ShipSteererComponent>(ent);

        ent.Comp.Coordinates = coordinates;

        return ent.Comp;
    }

    /// <summary>
    /// Stops the steering behavior for the AI and cleans up.
    /// </summary>
    public void Stop(Entity<ShipSteererComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        RemComp<ShipSteererComponent>(ent);
    }
}
