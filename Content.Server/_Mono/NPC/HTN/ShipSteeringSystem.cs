using Content.Server.Physics.Controllers;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono;
using Content.Shared._Mono.SpaceArtillery;
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
    private EntityQuery<ProjectileGridPhaseComponent> _phaseQuery;
    private EntityQuery<PhysicsComponent> _physQuery;
    private EntityQuery<ShuttleComponent> _shuttleQuery;

    private List<Entity<MapGridComponent>> _avoidGrids = new();
    private HashSet<Entity<ShipWeaponProjectileComponent>> _avoidProjs = new();
    private List<(EntityUid Uid, bool IsGrid)> _avoidPotentialEnts = new();
    private List<ObstacleCandidate> _avoidEnts = new();

    // collision evasion input consideration sectors: 24 outer, 12 inner, 1 zero-input
    private List<EvadeCandidate> _sectors = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipSteererComponent, GetShuttleInputsEvent>(OnSteererGetInputs);
        SubscribeLocalEvent<ShipSteererComponent, PilotedShuttleRelayedEvent<StartCollideEvent>>(OnShuttleStartCollide);

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _phaseQuery = GetEntityQuery<ProjectileGridPhaseComponent>();
        _physQuery = GetEntityQuery<PhysicsComponent>();
        _shuttleQuery = GetEntityQuery<ShuttleComponent>();
    }

    private void OnSteererGetInputs(Entity<ShipSteererComponent> ent, ref GetShuttleInputsEvent args)
    {
        var pilotXform = Transform(ent);
        var shipUid = pilotXform.GridUid;

        var target = ent.Comp.Coordinates;
        var targetUid = target.EntityId;

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

        var targetXform = Transform(targetUid);
        var targetGrid = targetXform.GridUid;
        var mapTarget = _transform.ToMapCoordinates(target);
        var shipPos = _transform.GetMapCoordinates(shipXform);

        // we or target might just be in FTL so don't count us as finished
        if (mapTarget.MapId != shipPos.MapId)
            return;

        // gather context
        var shipNorthAngle = _transform.GetWorldRotation(shipXform);
        var toTargetVec = mapTarget.Position - shipPos.Position;
        var distance = toTargetVec.Length();
        var linVel = shipBody.LinearVelocity;
        var angVel = shipBody.AngularVelocity;

        var targetVel = Vector2.Zero;
        // if target doesn't have physcomp it's likely the map so keep vector as zero
        if (ent.Comp.LeadingEnabled && _physQuery.TryComp(targetGrid ?? targetUid, out var targetBody))
            targetVel = targetBody.LinearVelocity;
        var relVel = linVel - targetVel;

        // get the actual destination we will move to
        var destMapPos = ResolveDestination(ent.Comp, mapTarget, shipPos, shipNorthAngle, toTargetVec, distance, relVel, angVel);

        // ResolveDestination says we're all good
        if (ent.Comp.Status == ShipSteeringStatus.InRange)
            return;

        var config = new SteeringConfig
        {
            MaxArrivedVel = ent.Comp.InRangeMaxSpeed ?? float.PositiveInfinity,
            BrakeThreshold = ent.Comp.BrakeThreshold,
            TurnEaseIn = ent.Comp.TurnEaseIn,

            BaseEvasionTime = ent.Comp.BaseEvasionTime,
            AvoidCollisions = ent.Comp.AvoidCollisions,
            AvoidProjectiles = ent.Comp.AvoidProjectiles,
            EvasionSectorCount = ent.Comp.EvasionSectorCount,
            EvasionSectorDepth = ent.Comp.EvasionSectorDepth,
            MaxObstructorDistance = ent.Comp.MaxObstructorDistance,
            MinObstructorDistance = ent.Comp.MinObstructorDistance,
            EvasionBuffer = ent.Comp.EvasionBuffer,
            SearchBuffer = ent.Comp.GridSearchBuffer,
            ScanDistanceBuffer = ent.Comp.GridSearchDistanceBuffer,
            ProjectileSearchBounds = ent.Comp.ProjectileSearchBounds,

            RotationCompensationGain = ent.Comp.RotationCompensationGain,
            TargetAngleOffset = Angle.FromDegrees(ent.Comp.TargetRotation),
            AngleOverride = ent.Comp.AlwaysFaceTarget ? toTargetVec.ToWorldAngle() : null,
            AlwaysFaceTarget = ent.Comp.AlwaysFaceTarget
        };
        var context = new SteeringContext
        {
            ShipUid = shipUid.Value,
            ShipXform = shipXform,
            ShipBody = shipBody,
            Shuttle = shuttle,
            ShipGrid = shipGrid,
            ShipPos = shipPos,
            ShipNorthAngle = shipNorthAngle,

            DestMapPos = destMapPos,
            TargetVel = targetVel,
            TargetUid = targetUid,
            TargetEntPos = mapTarget,
            TargetGridUid = targetGrid,

            FrameTime = args.FrameTime
        };

        args.Input = ProcessMovement(context, config, ref ent.Comp.RotationCompensation);
    }

    /// <summary>
    /// Set our status and destination.
    /// </summary>
    private MapCoordinates ResolveDestination(
        ShipSteererComponent comp,
        MapCoordinates mapTarget,
        MapCoordinates shipPos,
        Angle shipNorthAngle,
        Vector2 toTargetVec,
        float distance,
        Vector2 relVel,
        float angVel)
    {
        var maxArrivedVel = comp.InRangeMaxSpeed ?? float.PositiveInfinity;
        var maxArrivedAngVel = comp.MaxRotateRate ?? float.PositiveInfinity;
        var targetAngleOffset = Angle.FromDegrees(comp.TargetRotation);

        var highRange = comp.Range + (comp.RangeTolerance ?? 0f);
        var lowRange = (comp.Range - comp.RangeTolerance) ?? 0f;
        var midRange = (highRange + lowRange) / 2f;

        switch (comp.Mode)
        {
            case ShipSteeringMode.GoToRange:
            {
                if (!comp.NoFinish
                    && distance >= lowRange && distance <= highRange
                    && relVel.Length() < maxArrivedVel
                    && MathF.Abs(angVel) < maxArrivedAngVel)
                {
                    var good = true;
                    if (comp.AlwaysFaceTarget)
                    {
                        var wishRotateBy = ShortestAngleDistance(shipNorthAngle + new Angle(Math.PI) - targetAngleOffset, toTargetVec.ToWorldAngle());
                        good = MathF.Abs((float)wishRotateBy.Theta) < comp.AlwaysFaceTargetOffset;
                    }
                    if (good)
                    {
                        comp.Status = ShipSteeringStatus.InRange;
                        return mapTarget; // will be ignored
                    }
                }

                if (distance < lowRange || distance > highRange)
                    return mapTarget.Offset(NormalizedOrZero(-toTargetVec) * midRange);

                return shipPos;
            }
            case ShipSteeringMode.OrbitCW:
            case ShipSteeringMode.Orbit:
            {
                // take our position, project onto our target radius, rotate by desired orbit offset
                var invert = comp.Mode == ShipSteeringMode.OrbitCW;
                var rotateAngle = new Angle(comp.OrbitOffset * (invert ? -1 : 1));
                return mapTarget.Offset(NormalizedOrZero(rotateAngle.RotateVec(-toTargetVec)) * midRange);
            }
        }

        return mapTarget;
    }

    /// <summary>
    /// Handle getting our inputs.
    /// </summary>
    private ShuttleInput ProcessMovement(
        in SteeringContext ctx,
        in SteeringConfig config,
        ref float rotationCompensation)
    {
        // check our braking power
        var brakeCtx = GetBrakeContext(ctx, config.MaxArrivedVel);

        // check obstacle avoidance
        ScanForObstacles(ctx, config, brakeCtx);
        var avoidanceVec = CalculateAvoidanceVector(ctx, config, brakeCtx);

        // use avoidance vector if available or proceed with thrust as normal
        var wishInputVec = avoidanceVec ?? CalculateNavigationVector(ctx, brakeCtx);

        // process angular input
        var rotControl = CalculateRotationControl(ctx, config, wishInputVec, ref rotationCompensation);

        // process brake input
        var brakeInput = CalculateBrake(ctx, config, wishInputVec, rotControl, brakeCtx);

        // convert wish-input to ship context
        var strafeInput = (-ctx.ShipNorthAngle).RotateVec(wishInputVec);
        strafeInput = GetGoodThrustVector(strafeInput, ctx.Shuttle) * MathF.Min(1f, wishInputVec.Length());
        // Log.Info($"input {strafeInput} norot {wishInputVec}");

        return new ShuttleInput(strafeInput, rotControl.RotationInput, brakeInput);
    }

    private BrakeContext GetBrakeContext(in SteeringContext ctx, float maxArrivedVel)
    {
        // check our brake thrust
        var brakeVec = GetGoodThrustVector((-ctx.ShipNorthAngle).RotateVec(-ctx.ShipBody.LinearVelocity), ctx.Shuttle);
        var brakeThrust = _mover.GetDirectionThrust(brakeVec, ctx.Shuttle, ctx.ShipBody) * ShuttleComponent.BrakeCoefficient;
        var brakeAccelVec = brakeThrust * ctx.ShipBody.InvMass;
        var brakeAccel = brakeAccelVec.Length();

        var linVelLenSq = ctx.ShipBody.LinearVelocity.LengthSquared();

        // s = v^2 / 2a
        var brakePath = linVelLenSq / (2f * brakeAccel);
        // path we will pass if we keep braking until we reach our desired max velocity
        var innerBrakePath = maxArrivedVel*maxArrivedVel / (2f * brakeAccel);

        // negative if we're already slow enough
        var leftoverBrakePath = brakeAccel == 0f ? 0f : brakePath - innerBrakePath;

        return new BrakeContext(brakeAccel, brakePath, leftoverBrakePath);
    }

    private void ScanForObstacles(in SteeringContext ctx, in SteeringConfig config, in BrakeContext brake)
    {
        var SearchBuffer = config.SearchBuffer;
        var ScanDistanceBuffer = config.ScanDistanceBuffer;
        var ProjectileSearchBounds = config.ProjectileSearchBounds;

        var shipPosVec = ctx.ShipPos.Position;
        var shipVel = ctx.ShipBody.LinearVelocity;
        var shipAABB = ctx.ShipGrid.LocalAABB;
        var velAngle = ctx.ShipBody.LinearVelocity.ToWorldAngle();

        var scanDistance = brake.BrakeAccel == 0f ?
                               config.MaxObstructorDistance
                               : MathF.Min(config.MaxObstructorDistance, brake.BrakePath * 2f);
        scanDistance += shipAABB.Size.Length() * 0.5f + ScanDistanceBuffer;

        var scanBoundsLocal = shipAABB
            .Enlarged(SearchBuffer)
            .ExtendToContain(new Vector2(0, scanDistance));

        var scanBounds = new Box2(scanBoundsLocal.BottomLeft + shipPosVec, scanBoundsLocal.TopRight + shipPosVec);
        var scanBoundsWorld = new Box2Rotated(scanBounds, velAngle - new Angle(Math.PI), shipPosVec);

        // query for everything nearby
        _avoidGrids.Clear();
        if (config.AvoidCollisions)
            _mapMan.FindGridsIntersecting(ctx.ShipPos.MapId, scanBoundsWorld, ref _avoidGrids, approx: true, includeMap: false);

        _avoidProjs.Clear();
        if (config.AvoidProjectiles)
            _avoidProjs = _lookup.GetEntitiesInRange<ShipWeaponProjectileComponent>(
                ctx.ShipPos, ProjectileSearchBounds, LookupFlags.Approximate | LookupFlags.Dynamic | LookupFlags.Sensors);

        // pool all queried ents
        _avoidPotentialEnts.Clear();
        foreach (var grid in _avoidGrids)
            _avoidPotentialEnts.Add((grid, true));

        foreach (var proj in _avoidProjs)
            if (!_phaseQuery.TryComp(proj, out var phase) || phase.SourceGrid != ctx.ShipUid)
                _avoidPotentialEnts.Add((proj, false));

        _avoidEnts.Clear();
        foreach (var (ent, isGrid) in _avoidPotentialEnts)
        {
            // don't avoid ourselves or the target
            if (ent == ctx.ShipUid || ent == ctx.TargetUid || ent == ctx.TargetGridUid || !_physQuery.TryComp(ent, out var obstacleBody))
                continue;

            var otherXform = Transform(ent);
            _gridQuery.TryComp(ent, out var obsGrid);
            var aabb = _physics.GetWorldAABB(ent, body: obstacleBody, xform: otherXform);
            var obsPos = aabb.Center;
            var obsRadius = (obsGrid?.LocalAABB ?? aabb).Size.Length() * 0.5f;

            _avoidEnts.Add(new((ent, otherXform, obstacleBody), obsPos, obsRadius, isGrid));
        }

    }

    private Vector2? CalculateAvoidanceVector(
        in SteeringContext ctx,
        in SteeringConfig config,
        in BrakeContext brake)
    {
        var shipPos = ctx.ShipPos.Position;
        var shipVel = ctx.ShipBody.LinearVelocity;
        var shipRadius = ctx.ShipGrid.LocalAABB.Size.Length() / 2f + config.EvasionBuffer;

        var targetVec = ctx.DestMapPos.Position - shipPos;
        var normTarget = NormalizedOrZero(targetVec);
        var wishDir = targetVec;
        wishDir.Normalize();

        // ignore collisions more than this far into the future
        // TODO: account for angular accel if we can't brake
        var simTime = brake.BrakeAccel == 0f ? 10f : 2f * ctx.ShipBody.LinearVelocity.Length() / brake.BrakeAccel;
        simTime += config.BaseEvasionTime;

        _sectors.Clear();
        for (var i = 0; i < config.EvasionSectorCount; i++)
        {
            var angle = Angle.FromDegrees(360f * i / (float)config.EvasionSectorCount);
            var dir = angle.ToVec();

            var rotated = (-ctx.ShipNorthAngle).RotateVec(dir);
            var dirAccel = _mover.GetDirectionThrust(rotated, ctx.Shuttle, ctx.ShipBody).Length();
            // if it's zero use a very rough approximation using our forward thrust
            if (dirAccel == 0f)
            {
                var upVec = new Vector2(0f, 1f);
                var penalty = 0.5f * (Vector2.Dot(upVec, rotated) + 1f);
                dirAccel = _mover.GetDirectionThrust(upVec, ctx.Shuttle, ctx.ShipBody).Length() * penalty;
            }

            for (var depth = 1; depth <= config.EvasionSectorDepth; depth++)
            {
                if (i % depth == 0)
                    _sectors.Add(new(dir, dirAccel / depth, 1f / depth));
            }
        }
        // set scale to -1 to mark it as the wish-sector
        _sectors.Add(new(wishDir, _mover.GetDirectionThrust((-ctx.ShipNorthAngle).RotateVec(wishDir), ctx.Shuttle, ctx.ShipBody).Length(), -1f));

        foreach (var obstacle in _avoidEnts)
        {
            var obsRadius = obstacle.Radius;
            var sumRadius = obsRadius + shipRadius;
            var obsXform = obstacle.Ent.Comp1;
            var obsPos = obstacle.Pos;
            var obsVel = obstacle.Ent.Comp2.LinearVelocity;
            var relVel = shipVel - obsVel;
            var toObsVec = obsPos - shipPos;
            var toObsDir = toObsVec.Normalized();
            var obsDistance = MathF.Max(toObsVec.Length() - sumRadius, 1f);
            // Log.Info($"DR {ToPrettyString(ctx.ShipUid)} Avoiding {ToPrettyString(obstacle.Ent)}: dp {toObsVec} dv {relVel} dist {obsDistance} sr {sumRadius}");

            // get time-to-collide with the accel of each sector
            //
            // r = obsDistance
            // d = sumRadius
            // p = vt + at^2 / 2
            // solve for: dot(p, toObsDir) = r
            // condition for no hit: abs(dot(p, toObsVec.rotate(90))) > d
            // p = (x, y)
            // toObsDir = (u, v)
            // ux + vy = r
            // x = v_x*t + a_x * t^2 / 2
            // y = v_y*t + a_y * t^2 / 2
            // u(v_x*t + 0.5*a_x*t^2) + v(v_y*t + 0.5*a_y*t^2) = r
            // t^2 * (0.5*(u*a_x + v*a_y)) + t * (u*v_x + v*v_y) - r = 0
            // k = 0.5 * u*a_x + v*a_y = 0.5 * dot(toObsDir, a)
            // l = u*v_x + v*v_y = dot(toObsDir, vel)
            // m = -r
            // t = (-l + sqrt(l^2 - 4km)) / (2k)
            // if 4km > l^2, no hit
            // if t<0, no hit
            //
            // https://www.desmos.com/calculator/foyraxlzs7 graphed version
            var l = Vector2.Dot(toObsDir, relVel);
            for (var i = 0; i < _sectors.Count; i++)
            {
                var sector = _sectors[i];

                var aDir = sector.Sector;
                var accel = aDir * sector.Accel;
                var k = 0.5f * Vector2.Dot(toObsDir, accel);
                var m = -obsDistance;
                var t = 4*k*m > l*l || k == 0f ? -1f : ((-l + MathF.Sqrt(l*l - 4*k*m)) * 0.5f / k);
                if (t < 0f || t > simTime)
                    continue;
                t = MathF.Max(0f, t - ctx.FrameTime);

                var endAt = relVel*t + 0.5f*accel*t*t;
                var proj = MathF.Abs(Vector2.Dot(endAt, new Vector2(-toObsDir.Y, toObsDir.X)));
                // Log.Info($"Avoid dir {aDir} time {t}, proj {proj} (k l m {k} {l} {m}) accel {accel}");
                if (proj > sumRadius)
                    continue;

                var ctime = sector.ImpactTime;
                if ((ctime == null || ctime > t) && (!sector.Priority || obstacle.IsGrid))
                {
                    var priority = obstacle.IsGrid || sector.Priority;
                    _sectors[i] = new(sector.Sector, sector.Accel, sector.Scale, t, priority);
                }
            }
            // specialcase 0, 0 wishInput
            var last = _sectors[_sectors.Count - 1];
            if (last.Sector.LengthSquared() == 0f)
            {
                var t = obsDistance / Vector2.Dot(relVel, toObsDir);
                if (t < 0f || t > simTime)
                    continue;

                var endAt = relVel*t;
                var proj = MathF.Abs(Vector2.Dot(endAt, new Vector2(-toObsDir.Y, toObsDir.X)));
                // Log.Info($"Avoid dir {last.Sector} time {t}, proj {proj}");
                if (proj > sumRadius)
                    continue;

                var ctime = last.ImpactTime;
                if ((ctime == null || ctime > t) && (!last.Priority || obstacle.IsGrid))
                {
                    var priority = obstacle.IsGrid || last.Priority;
                    _sectors[_sectors.Count - 1] = new(last.Sector, last.Accel, last.Scale, t, priority);
                }
            }
        }

        var closestSector = (int?)null;
        var closestDistance = float.PositiveInfinity;

        var bestSector = 0;
        var bestTime = 0f;
        for (var i = 0; i < _sectors.Count; i++)
        {
            var sector = _sectors[i];
            if (sector.ImpactTime == null)
            {
                var toWishSq = (wishDir - sector.Sector).LengthSquared();
                // Log.Info($"NI dir [{i}] {sector.Sector}: sq: {toWishSq} vs {closestDistance}");
                if (toWishSq < closestDistance)
                {
                    // Log.Info($"B: NI");
                    closestDistance = toWishSq;
                    closestSector = i;
                }
            }
            else
            {
                // Log.Info($"IT dir [{i}] {sector.Sector}: impact time {sector.ImpactTime.Value} vs {bestTime}");
                if (sector.ImpactTime.Value > bestTime)
                {
                    // Log.Info($"B: IT");
                    bestSector = i;
                    bestTime = sector.ImpactTime.Value;
                }
            }
        }

        var chosenI = closestSector ?? bestSector;
        var chosen = _sectors[chosenI];
        // Log.Info($"Chosen: [{chosenI}] {chosen.Sector}");
        // original wishDir is clear
        if (chosen.Scale == -1f)
            return null;

        return chosen.Sector * chosen.Scale;
    }

    // navigation for if we aren't avoiding a collision
    private Vector2 CalculateNavigationVector(in SteeringContext ctx, in BrakeContext brake)
    {
        var toDestVec = ctx.DestMapPos.Position - ctx.ShipPos.Position;
        var destDistance = toDestVec.Length();
        var toDestDir = NormalizedOrZero(toDestVec);
        var relVel = ctx.ShipBody.LinearVelocity - ctx.TargetVel;

        // we're good
        if (brake.LeftoverBrakePath < 0f && destDistance == 0f)
            return Vector2.Zero;

        // check if we should just brake
        if (brake.LeftoverBrakePath > destDistance)
            return -relVel;

        var linVelDir = NormalizedOrZero(relVel);

        // mirror linVelDir in relation to toTargetDir
        var adjustVec = -(linVelDir - toDestDir * Vector2.Dot(linVelDir, toDestDir));
        var adjustDir = NormalizedOrZero(adjustVec);

        var wishThrustDir = toDestDir + 2f * adjustVec;

        var wishThrustVec = _mover.GetDirectionThrust((-ctx.ShipNorthAngle).RotateVec(wishThrustDir), ctx.Shuttle, ctx.ShipBody);
        var adjustAccel = Vector2.Dot(adjustDir, wishThrustVec) * ctx.ShipBody.InvMass;

        var maxAdjust = Vector2.Dot(-adjustDir, relVel);

        adjustVec *= adjustAccel == 0f ? 0f : float.Clamp(maxAdjust / (adjustAccel * ctx.FrameTime), 0f, 1f);

        // do not yet process whether we can actually accelerate well in that direction
        return toDestDir + 2f * adjustVec;
    }

    private readonly record struct RotationResult(float RotationInput, float WishAngleVel);

    private RotationResult CalculateRotationControl(
        in SteeringContext ctx,
        in SteeringConfig config,
        Vector2 wishInputVec,
        ref float rotationCompensation)
    {
        Angle wishAngleActual;
        if (config.AngleOverride != null)
            wishAngleActual = config.AngleOverride.Value;
        else if (wishInputVec.Length() > 0)
            wishAngleActual = wishInputVec.ToWorldAngle();
        else
            wishAngleActual = (ctx.DestMapPos.Position - ctx.ShipPos.Position).ToWorldAngle();

        wishAngleActual += config.TargetAngleOffset;
        var wishAngle = wishAngleActual + rotationCompensation;

        var angAccel = _mover.GetAngularAcceleration(ctx.Shuttle, ctx.ShipBody);

        // process the PID
        var wishRotateByActual = ShortestAngleDistance(ctx.ShipNorthAngle + new Angle(Math.PI), wishAngleActual);
        rotationCompensation += (float)wishRotateByActual * config.RotationCompensationGain * ctx.FrameTime * MathF.Sqrt(angAccel);

        // process how we want to rotate
        var wishRotateBy = ShortestAngleDistance(ctx.ShipNorthAngle + new Angle(Math.PI), wishAngle);
        var wishAngleVel = MathF.Sqrt(MathF.Abs((float)wishRotateBy) * 2f * angAccel) * Math.Sign(wishRotateBy);

        // check by how much our desired angular velocity would rotate us in a frame
        var wishFrameRotate = wishAngleVel * ctx.FrameTime;
        // if that would overshoot the target, wish to rotate slower
        if (MathF.Abs(wishFrameRotate) > MathF.Abs((float)wishRotateBy) * config.TurnEaseIn && wishFrameRotate != 0f)
            wishAngleVel *= MathF.Abs((float)wishRotateBy * config.TurnEaseIn / wishFrameRotate);

        var wishDeltaAngleVel = wishAngleVel - ctx.ShipBody.AngularVelocity;
        // this is clamped to [-1, 1] downstream, but need to invert input
        var rotationInput = angAccel == 0f ? 0f : -wishDeltaAngleVel / angAccel / ctx.FrameTime;

        return new RotationResult(rotationInput, wishAngleVel);
    }

    private float CalculateBrake(
        in SteeringContext ctx,
        in SteeringConfig config,
        Vector2 wishInputVec,
        RotationResult rot,
        in BrakeContext brake)
    {

        var brakeInput = 0f;
        var linVel = ctx.ShipBody.LinearVelocity;
        var angleVel = ctx.ShipBody.AngularVelocity;

        // brake if we're:
        //   moving opposite to desired direction
        //   && not wanting to rotate much or want to brake our rotation as well
        if (Vector2.Dot(NormalizedOrZero(wishInputVec), NormalizedOrZero(-linVel)) >= config.BrakeThreshold
            && (MathF.Abs(rot.RotationInput) < 1f - config.BrakeThreshold
                || rot.WishAngleVel * angleVel < 0
                || MathF.Abs(rot.WishAngleVel) < MathF.Abs(angleVel)))
        {
            brakeInput = 1f;
        }

        return brakeInput;
    }

    private void OnShuttleStartCollide(Entity<ShipSteererComponent> ent, ref PilotedShuttleRelayedEvent<StartCollideEvent> outerArgs)
    {
        var args = outerArgs.Args;
        var targetEnt = ent.Comp.Coordinates.EntityId;
        var targetGrid = Transform(targetEnt).GridUid;

        // if we want to finish movement on collide with target, do so
        if (ent.Comp.FinishOnCollide && (args.OtherEntity == targetGrid || args.OtherEntity == targetEnt))
            ent.Comp.Status = ShipSteeringStatus.InRange;
    }

    // RT's equivalent method is broken so have to use this
    public static Angle ShortestAngleDistance(Angle from, Angle to)
    {
        var diff = (to - from) % Math.Tau;
        return diff + Math.Tau * (diff < -Math.PI ? 1 : diff > Math.PI ? -1 : 0);
    }

    public static Vector2 NormalizedOrZero(Vector2 vec)
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

        return NormalizedOrZero(res);
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

    private record struct SteeringContext
    {
        // ship
        public EntityUid ShipUid;
        public TransformComponent ShipXform;
        public PhysicsComponent ShipBody;
        // TODO: get rid of Shuttle and ShipGrid so this can be reused for non-grid piloting
        public ShuttleComponent Shuttle;
        public MapGridComponent ShipGrid;
        public MapCoordinates ShipPos;
        public Angle ShipNorthAngle;
        public MapCoordinates DestMapPos;
        // target
        public Vector2 TargetVel;
        public EntityUid TargetUid;
        public EntityUid? TargetGridUid;
        public MapCoordinates TargetEntPos;
        // misc
        public float FrameTime;
    }

    private record struct SteeringConfig
    {
        // movement
        public float MaxArrivedVel;
        public float BrakeThreshold;
        public float TurnEaseIn;
        // avoidance
        public bool AvoidCollisions;
        public bool AvoidProjectiles;
        public int EvasionSectorCount;
        public int EvasionSectorDepth;
        public float BaseEvasionTime;
        public float MaxObstructorDistance;
        public float MinObstructorDistance;
        public float EvasionBuffer;
        public float SearchBuffer;
        public float ScanDistanceBuffer;
        public float ProjectileSearchBounds;
        // PID
        public float RotationCompensationGain;
        // rotation
        public Angle TargetAngleOffset;
        public Angle? AngleOverride;
        public bool AlwaysFaceTarget;
    }

    private readonly record struct BrakeContext(float BrakeAccel, float BrakePath, float LeftoverBrakePath);

    private readonly record struct ObstacleCandidate(Entity<TransformComponent, PhysicsComponent> Ent, Vector2 Pos, float Radius, bool IsGrid);

    private record struct EvadeCandidate(Vector2 Sector, float Accel, float Scale, float? ImpactTime = null, bool Priority = false);
}
