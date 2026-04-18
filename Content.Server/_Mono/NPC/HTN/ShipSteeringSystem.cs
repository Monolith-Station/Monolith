using Content.Server._Mono.Projectiles.TargetSeeking;
using Content.Server.Physics.Controllers;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono;
using Content.Shared._Mono.SpaceArtillery;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
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
    [Dependency] private readonly TargetSeekingSystem _seeking = default!;

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
        var (destMapPos, inRange) = ResolveDestination(ent.Comp, mapTarget, shipPos, shipNorthAngle, toTargetVec, distance, relVel, angVel);

        // ResolveDestination says we're all good
        if (ent.Comp.Status == ShipSteeringStatus.InRange)
            return;

        Angle? targetAngle = inRange && ent.Comp.InRangeRotation is { } rot ? rot : (ent.Comp.AlwaysFaceTarget ? toTargetVec.ToWorldAngle() : null);

        var config = new SteeringConfig
        {
            MaxArrivedVel = ent.Comp.InRangeMaxSpeed ?? float.PositiveInfinity,
            BrakeThreshold = ent.Comp.BrakeThreshold,

            BaseEvasionTime = ent.Comp.BaseEvasionTime,
            AvoidCollisions = ent.Comp.AvoidCollisions,
            AvoidProjectiles = ent.Comp.AvoidProjectiles,
            AvoidanceNoRotate = ent.Comp.AvoidanceNoRotate,
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
            AngleOverride = targetAngle
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
    private (MapCoordinates, bool) ResolveDestination(
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
                    if (comp.InRangeRotation is { } targetWorldRot)
                    {
                        var wishRotateBy = ShortestAngleDistance(shipNorthAngle + new Angle(Math.PI), targetWorldRot);
                        good = MathF.Abs((float)wishRotateBy.Theta) < comp.RotationTolerance;
                    }
                    else if (comp.AlwaysFaceTarget)
                    {
                        var wishRotateBy = ShortestAngleDistance(shipNorthAngle + new Angle(Math.PI) - targetAngleOffset, toTargetVec.ToWorldAngle());
                        good = MathF.Abs((float)wishRotateBy.Theta) < comp.RotationTolerance;
                    }
                    if (good)
                    {
                        comp.Status = ShipSteeringStatus.InRange;
                        return (mapTarget, true); // will be ignored
                    }
                }

                if (distance < lowRange || distance > highRange)
                    return (mapTarget.Offset(NormalizedOrZero(-toTargetVec) * midRange), false);

                return (shipPos, true);
            }
            case ShipSteeringMode.OrbitCW:
            case ShipSteeringMode.Orbit:
            {
                // take our position, project onto our target radius, rotate by desired orbit offset
                var invert = comp.Mode == ShipSteeringMode.OrbitCW;
                var rotateAngle = new Angle(comp.OrbitOffset * (invert ? -1 : 1));
                return (mapTarget.Offset(NormalizedOrZero(rotateAngle.RotateVec(-toTargetVec)) * midRange), false);
            }
        }

        return (mapTarget, false);
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

        var navVec = CalculateNavigationVector(ctx, brakeCtx);

        // check obstacle avoidance
        ScanForObstacles(ctx, config, brakeCtx);
        var avoidanceVec = CalculateAvoidanceVector(ctx, config, brakeCtx, navVec);

        // use avoidance vector if available or proceed with thrust as normal
        var wasNav = navVec;
        var wishInputVec = avoidanceVec ?? navVec;

        var rotWish = wishInputVec;
        if (avoidanceVec != null && config.AvoidanceNoRotate)
            rotWish = wasNav;

        // process angular input
        var rotControl = CalculateRotationControl(ctx, config, rotWish, ref rotationCompensation);

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
        var brakeAccelVec = _mover.GetDirectionAccel(brakeVec, ctx.Shuttle, ctx.ShipBody, ctx.ShipXform);
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
                               : MathF.Min(config.MaxObstructorDistance, brake.BrakePath * 4f);
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
        in BrakeContext brake,
        Vector2 wishDir)
    {
        var shipPos = ctx.ShipPos.Position;
        var shipVel = ctx.ShipBody.LinearVelocity;
        var shipRadius = ctx.ShipGrid.LocalAABB.Size.Length() / 2f + config.EvasionBuffer;

        var targetVec = ctx.DestMapPos.Position - shipPos;
        var normTarget = NormalizedOrZero(targetVec);

        // ignore collisions more than this far into the future
        var simTime = brake.BrakeAccel == 0f ? 10f : 2f * ctx.ShipBody.LinearVelocity.Length() / brake.BrakeAccel;
        simTime += config.BaseEvasionTime;

        var forwardAccelVec = _mover.GetDirectionAccel(new Vector2(0f, 1f), ctx.Shuttle, ctx.ShipBody, ctx.ShipXform);
        var forwardAccelDir = NormalizedOrZero(forwardAccelVec);
        var forwardAccel = forwardAccelVec.Length();

        _sectors.Clear();
        for (var i = 0; i < config.EvasionSectorCount; i++)
        {
            var angle = Angle.FromDegrees(360f * i / (float)config.EvasionSectorCount);
            var dir = angle.ToVec();

            var dirAccel = _mover.GetWorldDirectionAccel(dir, ctx.Shuttle, ctx.ShipBody, ctx.ShipXform);
            if (dirAccel.LengthSquared() == 0f) {
                dirAccel = dir * forwardAccel * (Vector2.Dot(dir, forwardAccelDir) + 1) * 0.5f;
            }

            for (var depth = 1; depth <= config.EvasionSectorDepth; depth++)
            {
                if (i % depth == 0)
                    _sectors.Add(new(dirAccel / depth, 1f / depth));
            }
        }
        // set scale to -1 to mark it as the wish-sector
        var wishDirThrust = _mover.GetWorldDirectionAccel(wishDir, ctx.Shuttle, ctx.ShipBody, ctx.ShipXform);
        _sectors.Add(new(wishDirThrust, -1f));

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

            // Distances to bounding planes
            var obsDistanceFront = MathF.Max(toObsVec.Length() - sumRadius, 1f);
            var obsDistanceCenter = toObsVec.Length();

            var l = Vector2.Dot(toObsDir, relVel);
            for (var i = 0; i < _sectors.Count; i++)
            {
                var sector = _sectors[i];

                var accel = sector.Accel;
                var k = 0.5f * Vector2.Dot(toObsDir, accel);

                // 1. Solve crossing the Front Tangent Plane (Entering the obstacle bounds)
                float t_f;
                if (k * k < l * l / 1024f)
                    t_f = l != 0f ? obsDistanceFront / l : -1f;
                else
                {
                    var desc_f = l * l + 4f * k * obsDistanceFront;
                    t_f = desc_f < 0f || k == 0f ? -1f : ((-l + MathF.Sqrt(desc_f)) * 0.5f / k);
                }

                if (t_f < 0f || t_f > simTime)
                    continue;

                // 2. Resolve longitudinal exit/stop trajectory
                Vector2 p_end;
                if (k * k < l * l / 1024f)
                {
                    float t_c = l != 0f ? obsDistanceCenter / l : -1f;
                    if (t_c < 0f) t_c = t_f; // Failsafe bounds
                    p_end = relVel * t_c + 0.5f * accel * t_c * t_c;
                }
                else
                {
                    var desc_c = l * l + 4f * k * obsDistanceCenter;
                    if (desc_c < 0f)
                    {
                        // The ship stops longitudinally inside the front half of the obstacle
                        var t_stop = -l / (2f * k);
                        p_end = relVel * t_stop + 0.5f * accel * t_stop * t_stop;
                    }
                    else
                    {
                        var t_c = ((-l + MathF.Sqrt(desc_c)) * 0.5f / k);
                        if (t_c < 0f) t_c = t_f; // Failsafe bounds
                        p_end = relVel * t_c + 0.5f * accel * t_c * t_c;
                    }
                }

                // 3. Line-segment to Circle-Center intersection.
                // Represents exact path traveled while navigating across the physical dimension of the obstacle.
                var p_start = relVel * t_f + 0.5f * accel * t_f * t_f;
                var seg = p_end - p_start;
                var l2 = seg.LengthSquared();
                var hits = false;

                if (l2 == 0f)
                {
                    if ((p_start - toObsVec).LengthSquared() <= sumRadius * sumRadius)
                        hits = true;
                }
                else
                {
                    // Find closest point on the segment to the obstacle's actual center point
                    var t_seg = Math.Clamp(Vector2.Dot(toObsVec - p_start, seg) / l2, 0f, 1f);
                    var proj = p_start + seg * t_seg;
                    if ((proj - toObsVec).LengthSquared() <= sumRadius * sumRadius)
                        hits = true;
                }

                if (!hits)
                    continue;

                var t = MathF.Max(0f, t_f - ctx.FrameTime);

                var ctime = sector.ImpactTime;
                if ((ctime == null || ctime > t) && (!sector.Priority || obstacle.IsGrid))
                {
                    var priority = obstacle.IsGrid || sector.Priority;
                    _sectors[i] = new(sector.Accel, sector.Scale, t, priority);
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
                var toWishSq = (wishDir - NormalizedOrZero(sector.Accel) * sector.Scale).LengthSquared();
                if (toWishSq < closestDistance)
                {
                    closestDistance = toWishSq;
                    closestSector = i;
                }
            }
            else
            {
                if (sector.ImpactTime.Value > bestTime)
                {
                    bestSector = i;
                    bestTime = sector.ImpactTime.Value;
                }
            }
        }

        var chosenI = closestSector ?? bestSector;
        var chosen = _sectors[chosenI];
        // original wishDir is clear
        if (chosen.Scale == -1f)
            return null;

        return NormalizedOrZero(chosen.Accel) * chosen.Scale;
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

        var linVelDir = NormalizedOrZero(relVel);

        // check if we should just brake
        if (brake.LeftoverBrakePath > destDistance)
            return -linVelDir;

        var accelEstVec = _mover.GetWorldDirectionAccel(toDestVec, ctx.Shuttle, ctx.ShipBody, ctx.ShipXform);
        var accelEst = accelEstVec.Length();
        // fallback to forward accel
        if (accelEst == 0f)
        {
            var forwardAccelVec = _mover.GetDirectionAccel(new Vector2(0f, 1f), ctx.Shuttle, ctx.ShipBody, ctx.ShipXform);
            accelEst = forwardAccelVec.Length();
        }
        // takes target relative to us
        var wishAngle = _seeking.CalculateAdvancedTracking(toDestVec, -relVel, accelEst);

        // do not yet process whether we can actually accelerate well in that direction
        return wishAngle.ToWorldVec();
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
        if (MathF.Abs(wishFrameRotate) > MathF.Abs((float)wishRotateBy) && wishFrameRotate != 0f)
            wishAngleVel *= MathF.Abs((float)wishRotateBy / wishFrameRotate);

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
        // avoidance
        public bool AvoidCollisions;
        public bool AvoidProjectiles;
        public bool AvoidanceNoRotate;
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
    }

    private readonly record struct BrakeContext(float BrakeAccel, float BrakePath, float LeftoverBrakePath);

    private readonly record struct ObstacleCandidate(Entity<TransformComponent, PhysicsComponent> Ent, Vector2 Pos, float Radius, bool IsGrid);

    private record struct EvadeCandidate(Vector2 Accel, float Scale, float? ImpactTime = null, bool Priority = false);
}
