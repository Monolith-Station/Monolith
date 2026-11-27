using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono.Ships;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Mono.Ships;

public sealed partial class ShipShieldCollisionSystem
{
    private readonly Dictionary<EntityUid, GridStart> _gridStarts = new();
    private const float RimTolerance = 0.000001f;

    private void OnBeforeSolve(ref PhysicsUpdateBeforeSolveEvent args)
    {
        _gridStarts.Clear();
        if (_shells.Count == 0)
            return;

        var grids = EntityQueryEnumerator<MapGridComponent, PhysicsComponent, TransformComponent>();
        while (grids.MoveNext(out var grid, out _, out var body, out var transform))
        {
            if (!body.CanCollide || HasComp<MapComponent>(grid))
                continue;

            var thrust = TryComp(grid, out ShuttleComponent? shuttle) ? shuttle.LastThrust : Vector2.Zero;
            _gridStarts.Add(grid, new GridStart(ReadPose(transform, body), transform.ParentUid,
                thrust + body.Force * body.InvMass, body.Torque * body.InvI));
        }
    }

    private void OnAfterSolve(ref PhysicsUpdateAfterSolveEvent args)
    {
        if (_gridStarts.Count == 0)
            return;

        BlockShieldPairs();
        foreach (var (uid, shell) in _shells)
        {
            if (TryComp(uid, out ShipShieldProjectorComponent? shield) &&
                shield.Shield.Blocks(ShipShieldCollision.Grids) && !IsPaused(uid))
                BlockGrids((uid, shield), shell);
        }
    }

    private void BlockGrids(Entity<ShipShieldProjectorComponent> shield, Shell shell)
    {
        var owner = shell.Grid ?? shield.Owner;
        TryComp(owner, out PhysicsComponent? ownerBody);
        var movesShell = _gridStarts.TryGetValue(owner, out var ownerStart) && Transform(owner).ParentUid == ownerStart.Parent;

        foreach (var (grid, start) in _gridStarts)
        {
            if (grid == owner || !TryComp(grid, out PhysicsComponent? body) ||
                !TryComp(grid, out MapGridComponent? gridComp))
                continue;

            var xform = Transform(grid);
            var altitude = _levels.GetAbsoluteAltitude(grid);
            var inverseMass = body.InvMass + (ownerBody?.InvMass ?? 0f);
            if (xform.ParentUid != start.Parent ||
                !_containment.TryGetSlice(shield, xform.MapUid, altitude, out var slice))
                continue;

            var (center, rotation) = _transform.GetWorldPositionRotation(shield);
            var fixedPose = new GridPose(center, rotation, Vector2.Zero);
            var ownerEnd = movesShell ? ReadPose(Transform(owner), ownerBody!) : fixedPose;
            var host = new GridMotion(movesShell ? ownerStart.Pose : fixedPose, ownerEnd);
            var hull = new GridMotion(start.Pose, ReadPose(xform, body));
            var offset = (-ownerEnd.Rotation).RotateVec(center - ownerEnd.Center);
            var angle = rotation - ownerEnd.Rotation;
            var travel = (hull.Travel - host.Travel).Length();
            var bounds = gridComp.LocalAABB;
            var farCorner = Vector2.Max(Vector2.Abs(bounds.BottomLeft - body.LocalCenter),
                Vector2.Abs(bounds.TopRight - body.LocalCenter));
            var radius = MathF.Max(slice.X, slice.Y) + host.TurnSize * offset.Length() +
                travel + hull.TurnSize * farCorner.Length();
            var sweptArea = Box2.CenteredAround(center, new Vector2(radius * 2f));
            if (!_transform.GetWorldMatrix(xform).TransformBox(bounds).Intersects(sweptArea))
                continue;

            var first = 1f;
            var impact = Vector2.Zero;
            var inside = false;
            var atStart = ToShell(0f);
            var translating = hull.TurnSize == 0f && host.TurnSize == 0f;
            var relativeStep = translating ? Vector2.TransformNormal(hull.Travel - host.Travel, atStart) : Vector2.Zero;
            var startCenter = Vector2.Transform(body.LocalCenter, atStart);
            var tiles = _maps.GetTilesIntersecting(grid, gridComp, sweptArea);
            var vertices = new Vector2[4];
            foreach (var tile in tiles)
            {
                SharedShipShieldShellSystem.WriteGridTileVertices(tile.GridIndices, gridComp.TileSize, vertices);
                var reach = 0f;
                foreach (var vertex in vertices)
                    reach = MathF.Max(reach, Vector2.Distance(vertex, body.LocalCenter));

                // Conservative advancement bounds the entire rotating edge, not just its endpoints.
                var hostReach = Vector2.Distance(hull.Start.Center, host.Start.Center) + travel + reach;
                var speed = (travel + hull.TurnSize * reach + host.TurnSize * hostReach) / MathF.Min(slice.X, slice.Y);
                if (speed < 0.00001f)
                    continue;

                SharedShipShieldShellSystem.GetFootprintExtrema(vertices, atStart, out var closest, out var farthest);
                var near = closest.Length();
                var far = farthest.Length();
                var fromInside = far <= 1f || near < 1f && startCenter.LengthSquared() < 1f;
                if (translating && !fromInside && near >= 1f - RimTolerance && Vector2.Dot(closest, relativeStep) >= 0f)
                    continue;

                if (translating && fromInside && far <= 1f + RimTolerance)
                {
                    // Interior containment along a straight segment is convex.
                    SharedShipShieldShellSystem.GetFootprintExtrema(vertices, ToShell(first), out _, out var end);
                    if (end.LengthSquared() <= 1f)
                        continue;
                }

                var clearance = fromInside ? 1f - far : near - 1f;
                var limit = MathF.Min(0f, clearance);
                var time = 0f;
                var pointOnHull = fromInside ? farthest : closest;
                for (var i = 0; i < 128 && time < first; i++)
                {
                    var step = (clearance - limit + RimTolerance) / speed;
                    if (step >= first - time)
                    {
                        time = first;
                        break;
                    }

                    if (step <= 0f)
                        break;

                    var next = time + step * 0.9f;
                    SharedShipShieldShellSystem.GetFootprintExtrema(vertices, ToShell(next), out closest, out farthest);
                    var nextClearance = fromInside ? 1f - farthest.Length() : closest.Length() - 1f;
                    if (nextClearance < limit)
                        break;

                    time = next;
                    clearance = nextClearance;
                    pointOnHull = fromInside ? farthest : closest;
                }

                if (time >= first)
                    continue;
                first = time;
                impact = pointOnHull;
                inside = fromInside;
            }

            // A sweep can't undo an overlap it started with. Separate it like a wall contact.
            var recovery = 0f;
            var atContact = ToShell(first);
            foreach (var tile in tiles)
            {
                SharedShipShieldShellSystem.WriteGridTileVertices(tile.GridIndices, gridComp.TileSize, vertices);
                SharedShipShieldShellSystem.GetFootprintExtrema(vertices, atStart, out var startNear, out var startFar);
                var near = startNear.Length();
                var far = startFar.Length();
                if (near >= 1f - RimTolerance || far <= 1f + RimTolerance)
                    continue;

                var wasInside = startCenter.LengthSquared() < 1f;
                SharedShipShieldShellSystem.GetFootprintExtrema(vertices, atContact, out var closest, out var farthest);
                var pointOnHull = wasInside ? farthest : closest;
                var length = pointOnHull.Length();
                var depth = wasInside ? length - 1f : 1f - length;
                if (length <= RimTolerance || depth <= recovery + RimTolerance)
                    continue;

                recovery = depth;
                inside = wasInside;
                impact = pointOnHull;
            }

            if (impact.LengthSquared() < 0.00001f || inverseMass <= 0f)
                continue;

            var gridPose = hull.At(first);
            var ownerPose = host.At(first);
            var shellRotation = ownerPose.Rotation + angle;
            var rim = Vector2.Normalize(impact);
            var point = ownerPose.Center + ownerPose.Rotation.RotateVec(offset) + shellRotation.RotateVec(rim * slice);
            var normal = shellRotation.RotateVec(Vector2.Normalize(rim / slice)) * (inside ? -1f : 1f);
            var gridArm = point - gridPose.Center;
            var ownerArm = point - ownerPose.Center;
            var gridLever = Vector2Helpers.Cross(gridArm, normal);
            var ownerLever = Vector2Helpers.Cross(ownerArm, normal);
            var linearClosing = -Vector2.Dot(_physics.GetMapLinearVelocity(grid, body) -
                _physics.GetMapLinearVelocity(owner, ownerBody), normal);
            var gridTurn = body.AngularVelocity * gridLever;
            var ownerTurn = (ownerBody?.AngularVelocity ?? 0f) * ownerLever;
            var closing = linearClosing - gridTurn + ownerTurn;
            if (linearClosing > 0f && inverseMass > 0f)
            {
                var impulse = normal * (linearClosing / inverseMass);
                _physics.ApplyLinearImpulse(grid, impulse, body: body);
                if (ownerBody != null)
                    _physics.ApplyLinearImpulse(owner, -impulse, body: ownerBody);
            }

            // Stop the blocked turn instead of turning it into an outward linear kick.
            if (gridTurn < 0f)
                _physics.SetAngularVelocity(grid, 0f, body: body);
            if (ownerBody != null && ownerTurn > 0f)
                _physics.SetAngularVelocity(owner, 0f, body: ownerBody);

            // Keep shared translation after contact; rewinding it pins two touching ships in place.
            var carry = movesShell && ownerBody!.InvMass > 0f && body.InvMass > 0f
                ? (hull.Travel * ownerBody.InvMass + host.Travel * body.InvMass) * ((1f - first) / inverseMass)
                : Vector2.Zero;
            var displacement = shellRotation.RotateVec(rim * slice) * (inside ? -recovery : recovery);
            var ownerShift = movesShell ? displacement * (ownerBody!.InvMass / inverseMass) : Vector2.Zero;
            var gridShift = displacement - ownerShift;
            if (gridShift != Vector2.Zero || first < 1f && (carry != Vector2.Zero || hull.Travel != Vector2.Zero || hull.TurnSize > 0f))
                RestorePose(grid, xform, body, gridPose with { Center = gridPose.Center + carry + gridShift });
            if (movesShell && (ownerShift != Vector2.Zero || first < 1f &&
                (carry != Vector2.Zero || host.Travel != Vector2.Zero || host.TurnSize > 0f)))
                RestorePose(owner, Transform(owner), ownerBody!, ownerPose with { Center = ownerPose.Center + carry - ownerShift });

            var drive = start.Thrust - ownerStart.Thrust;
            var pushing = Vector2.Dot(drive, normal) + start.TurnDrive * gridLever - ownerStart.TurnDrive * ownerLever < -0.01f;
            if (pushing || closing > 0.01f)
            {
                var height = shell.UsesAltitude ? _containment.GetSurfaceHeight(shield, altitude) : 0f;
                var contact = new ShipShieldSurfaceContactEvent(new Vector3(rim * slice / shell.Radii, height), xform.MapID, pushing, grid);
                OnSurfaceContact(shield, ref contact);
            }

            Matrix3x2 ToShell(float fraction)
            {
                var gridAt = hull.At(fraction);
                var hostAt = host.At(fraction);
                var shellCenter = hostAt.Center + hostAt.Rotation.RotateVec(offset);
                return Matrix3x2.CreateTranslation(-gridAt.LocalCenter) *
                    Matrix3x2.CreateRotation((float) gridAt.Rotation.Theta) *
                    Matrix3x2.CreateTranslation(gridAt.Center - shellCenter) *
                    Matrix3x2.CreateRotation(-(float) (hostAt.Rotation + angle).Theta) *
                    Matrix3x2.CreateScale(Vector2.One / slice);
            }
        }
    }

    private GridPose ReadPose(TransformComponent transform, PhysicsComponent body)
    {
        var (position, rotation) = _transform.GetWorldPositionRotation(transform.Owner);
        return new GridPose(position + rotation.RotateVec(body.LocalCenter), rotation, body.LocalCenter);
    }

    private void RestorePose(EntityUid uid, TransformComponent transform, PhysicsComponent body, GridPose pose)
    {
        _transform.SetWorldPositionRotation(uid, pose.Center - pose.Rotation.RotateVec(pose.LocalCenter), pose.Rotation, transform);
        foreach (var (emitter, shell) in _shells)
        {
            if (!shell.Welded || shell.Grid != uid || !TryComp(shell.Host, out PhysicsComponent? proxy))
                continue;

            // The proxy is a separate body. Rewind it too so the weld has no error to correct.
            var (position, rotation) = _transform.GetWorldPositionRotation(emitter);
            _transform.SetWorldPositionRotation(shell.Host, position, rotation);
            var arm = position + rotation.RotateVec(proxy.LocalCenter) - pose.Center;
            _physics.SetLinearVelocity(shell.Host, _physics.GetMapLinearVelocity(uid, body) +
                Vector2Helpers.Cross(body.AngularVelocity, arm), body: proxy);
            _physics.SetAngularVelocity(shell.Host, body.AngularVelocity, body: proxy);
        }
    }

    private readonly record struct GridStart(GridPose Pose, EntityUid Parent, Vector2 Thrust, float TurnDrive);

    private readonly record struct GridPose(Vector2 Center, Angle Rotation, Vector2 LocalCenter);

    private readonly struct GridMotion(GridPose start, GridPose end)
    {
        public readonly GridPose Start = start;
        public readonly Vector2 Travel = end.Center - start.Center;
        public readonly Angle Turn = Angle.ShortestDistance(start.Rotation, end.Rotation);
        public float TurnSize => (float) Math.Abs(Turn.Theta);

        public GridPose At(float t) => new(Start.Center + Travel * t, Start.Rotation + Turn * t, Start.LocalCenter);
    }
}
