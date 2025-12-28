using Content.Server.Physics.Controllers;
using Content.Server.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using System.Numerics;

namespace Content.Server._Mono.NPC.HTN;

public sealed partial class ShipSteeringSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly MoverController _mover = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<PhysicsComponent> _physQuery;
    private EntityQuery<ShuttleComponent> _shuttleQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipSteererComponent, GetShuttleInputsEvent>(OnSteererGetInputs);
        SubscribeLocalEvent<ShipSteererComponent, PilotedShuttleRelayedEvent<StartCollideEvent>>(OnShuttleStartCollide);

        _gridQuery = GetEntityQuery<MapGridComponent>();
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

        if (shipUid == null
            || TerminatingOrDeleted(targetUid)
            || !_shuttleQuery.TryComp(shipUid, out var shuttle)
            || !_physQuery.TryComp(shipUid, out var shipBody)
            || !_gridQuery.TryComp(shipUid, out var shipGrid))
        {
            ent.Comp.Status = ShipSteeringStatus.InRange;
            return;
        }
        ent.Comp.Status = ShipSteeringStatus.Moving;

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

        var destMapPos = mapTarget;

        switch (ent.Comp.Mode)
        {
            case ShipSteeringMode.GoToRange:
            {
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
                if (distance < lowRange || distance > highRange)
                    destMapPos = mapTarget.Offset(NormalizedOrZero(-toTargetVec) * midRange);
                else
                    destMapPos = shipPos;

                break;
            }
            case ShipSteeringMode.Orbit:
            {
                // target a position slightly offset from ours, have maxArrivedVel handle having proper velocity
                destMapPos = mapTarget.Offset(NormalizedOrZero(ent.Comp.OrbitOffset.RotateVec(-toTargetVec)) * midRange);
                break;
            }
        }

        args.Input = ProcessMovement(shipUid.Value,
                                     shipXform, shipBody, shuttle, shipGrid,
                                     destMapPos, targetVel, targetUid, mapTarget,
                                     maxArrivedVel, ent.Comp.BrakeThreshold, args.FrameTime,
                                     ent.Comp.AvoidCollisions, ent.Comp.MaxObstructorDistance,
                                     targetAngleOffset, ent.Comp.AlwaysFaceTarget ? toTargetVec.ToWorldAngle() : null);
    }

    private ShuttleInput ProcessMovement(EntityUid shipUid,
                                         TransformComponent shipXform, PhysicsComponent shipBody, ShuttleComponent shuttle, MapGridComponent shipGrid,
                                         MapCoordinates destMapPos, Vector2 targetVel, EntityUid? targetUid, MapCoordinates targetEntPos,
                                         float maxArrivedVel, float brakeThreshold, float frameTime,
                                         bool avoidCollisions, float maxObstructorDistance,
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

        Vector2 wishInputVec = Vector2.Zero;
        bool didCollisionAvoidance = false;
        // try avoid collisions
        if (avoidCollisions && linVel.LengthSquared() > 0f)
        {
            var grids = new List<Entity<MapGridComponent>>();

            // note: there's several magic numbers here, i consider those acceptable since they're almost an implementation detail
            // i can't think of any reason for anyone to want to change them
            const float SearchBuffer = 96f;
            const float ScanDistanceBuffer = 96f;
            const float CollisionRadiusBuffer = 12f;

            // how far ahead to look for grids
            var shipPosVec = shipPos.Position;
            var shipAABB = shipGrid.LocalAABB;
            var velAngle = linVel.ToWorldAngle();

            var scanDistance = (brakeAccel == 0f ? maxObstructorDistance : MathF.Min(maxObstructorDistance, brakePath))
                                + shipAABB.Height * 0.5f + ScanDistanceBuffer;

            var scanBoundsLocal = shipAABB
                                   .Enlarged(SearchBuffer)
                                   .ExtendToContain(new Vector2(0, scanDistance));

            var scanBounds = new Box2(scanBoundsLocal.BottomLeft + shipPosVec, scanBoundsLocal.TopRight + shipPosVec);
            var scanBoundsWorld = new Box2Rotated(scanBounds, velAngle - new Angle(Math.PI), shipPosVec);
            _mapMan.FindGridsIntersecting(shipPos.MapId, scanBoundsWorld, ref grids, approx: true, includeMap: false);

            foreach (var ent in grids)
            {
                if (ent.Owner == shipUid || ent.Owner == targetUid || !_physQuery.TryComp(ent, out var obstacleBody))
                    continue;

                var toObstacle = _transform.GetMapCoordinates(ent).Position - shipPosVec;
                var obstacleDistance = toObstacle.Length();

                var obstacleRelVel = linVel - obstacleBody.LinearVelocity;
                var velDir = NormalizedOrZero(linVel);
                var relVelDir = NormalizedOrZero(obstacleRelVel);

                // if it's somehow not in front of our movement we don't care
                if (Vector2.Dot(toObstacle, relVelDir) <= 0)
                    continue;

                // check by how much we have to miss
                // approximate via grid AABB
                var otherBounds = ent.Comp.LocalAABB;
                var shipRadius = MathF.Sqrt(shipAABB.Width * shipAABB.Width + shipAABB.Height * shipAABB.Height) / 2f + CollisionRadiusBuffer;
                var obstacleRadius = MathF.Sqrt(otherBounds.Width * otherBounds.Width + otherBounds.Height * otherBounds.Height) / 2f;
                var sumRadius = shipRadius + obstacleRadius;

                var targetEntDistance = (targetEntPos.Position - shipPos.Position).Length();
                // if it's behind destination entity we don't care, needed for ramming to work properly
                if (obstacleDistance > targetEntDistance + sumRadius)
                    continue;

                // check by how much we're already missing
                var effectiveDist = MathF.Max(obstacleDistance - sumRadius, 1f); // this being 0 will break things
                var pathVec = relVelDir * obstacleDistance * obstacleDistance / Vector2.Dot(toObstacle, relVelDir);
                var sideVec = pathVec - toObstacle;
                sideVec *= effectiveDist / obstacleDistance;
                var sideDist = sideVec.Length();

                if (sideDist < sumRadius)
                {
                    var toDestDir = NormalizedOrZero(toDestVec);

                    // get direction we want to dodge in and where we'll actually thrust to do that
                    var dodgeDir = NormalizedOrZero(sideVec);
                    var dodgeVec = GetGoodThrustVector((-shipNorthAngle).RotateVec(sideVec), shuttle);
                    var dodgeThrust = _mover.GetDirectionThrust(dodgeVec, shuttle, shipBody).Length();
                    var dodgeAccel = dodgeThrust * shipBody.InvMass;
                    var dodgeLeft = sumRadius - sideDist;
                    var dodgeVel = Vector2.Dot(obstacleRelVel, dodgeDir);
                    // knowing our side-thrust,
                    // solve quadratic equation to determine in how much more time we'll dodge
                    var dodgeTime = (-dodgeVel + MathF.Sqrt(dodgeVel * dodgeVel + 2f * dodgeLeft * dodgeAccel)) / dodgeAccel;

                    // check how much we can afford to thrust inwards (or outwards) anyway and still dodge
                    var inVel = Vector2.Dot(toObstacle, obstacleRelVel) * toObstacle / toObstacle.LengthSquared();
                    var maxInAccel = 2f * (effectiveDist / dodgeTime - inVel.Length()) / dodgeTime;

                    // check what's our actual inwards thrust so we know how to scale our input
                    var inAccelVec = GetGoodThrustVector((-shipNorthAngle).RotateVec(toDestDir), shuttle);
                    var inThrust = _mover.GetDirectionThrust(inAccelVec, shuttle, shipBody).Length();
                    var inAccel = inThrust * shipBody.InvMass;

                    // if we don't have dodge acceleration, brake and turn to the side and hope this helps
                    var inInput = dodgeAccel == 0f ? -1f : float.Clamp(maxInAccel / inAccel, -1f, 1f);

                    // those should be around perpendicular so this should work out
                    wishInputVec = toDestDir * inInput + dodgeDir;
                    didCollisionAvoidance = true;
                    break;
                }
            }
        }
        if (!didCollisionAvoidance)
        {
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
                var adjustVec = -(linVelDir - toDestDir * Vector2.Dot(linVelDir, toDestDir));
                var adjustDir = NormalizedOrZero(adjustVec);

                var adjustThrustDir = GetGoodThrustVector((-shipNorthAngle).RotateVec(adjustDir), shuttle);
                var adjustThrust = _mover.GetDirectionThrust(adjustVec, shuttle, shipBody).Length();
                var adjustAccel = adjustThrust * shipBody.InvMass;

                var adjustDirVel = Vector2.Dot(adjustDir, linVel) * adjustDir;
                adjustVec *= adjustAccel == 0f ? 0f : MathF.Min(1f, adjustDirVel.Length() / (adjustAccel * frameTime));

                wishInputVec = toDestDir + adjustVec * 2;
            }
        }

        var strafeInput = (-shipNorthAngle).RotateVec(wishInputVec);
        strafeInput = GetGoodThrustVector(strafeInput, shuttle);


        Angle wishAngle;
        if (angleOverride != null)
            wishAngle = angleOverride.Value;
        // try to face our thrust direction if we can
        // TODO: determine best thrust direction and face accordingly
        else if (wishInputVec.Length() > 0)
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
