using System.Numerics;
using Content.Shared._Mono.Ships;
using Robust.Shared.Physics.Components;

namespace Content.Server._Mono.Ships;

public sealed partial class ShipShieldCollisionSystem
{
    private void BlockShieldPairs()
    {
        foreach (var (uid, shell) in _shells)
        {
            if (!TryComp(uid, out ShipShieldProjectorComponent? shield) ||
                !shield.Shield.Blocks(ShipShieldCollision.Shields) || IsPaused(uid))
                continue;

            foreach (var (other, otherShell) in _shells)
            {
                if (other.Id <= uid.Id || IsPaused(other) ||
                    !TryComp(other, out ShipShieldProjectorComponent? otherShield) ||
                    !otherShield.Shield.Blocks(ShipShieldCollision.Shields))
                    continue;

                var a = shell.Grid ?? uid;
                var b = otherShell.Grid ?? other;
                if (a == b || !_gridStarts.ContainsKey(a) && !_gridStarts.ContainsKey(b))
                    continue;

                var mapA = Transform(uid);
                var mapB = Transform(other);
                var stack = _containment.GetStack(mapA.MapUid);
                if (stack != _containment.GetStack(mapB.MapUid) || stack == null && mapA.MapUid != mapB.MapUid)
                    continue;

                BlockShieldPair((uid, shield), (other, otherShield), a, b, stack != null);
            }
        }
    }

    private void BlockShieldPair(Entity<ShipShieldProjectorComponent> a, Entity<ShipShieldProjectorComponent> b,
        EntityUid ownerA, EntityUid ownerB, bool usesAltitude)
    {
        var motionA = ReadShellMotion(a, ownerA);
        var motionB = ReadShellMotion(b, ownerB);
        var mass = (motionA.Body?.InvMass ?? 0f) + (motionB.Body?.InvMass ?? 0f);
        if (mass <= 0f)
            return;

        var radiiA = a.Comp.Shield.Radii;
        var radiiB = b.Comp.Shield.Radii;
        var dz = usesAltitude
            ? _levels.GetAbsoluteAltitude(b) + b.Comp.Shield.CenterHeight -
              _levels.GetAbsoluteAltitude(a) - a.Comp.Shield.CenterHeight
            : 0f;
        if (MathF.Abs(dz) >= radiiA.Z + radiiB.Z)
            return;

        var reachA = MathF.Max(radiiA.X, radiiA.Y);
        var reachB = MathF.Max(radiiB.X, radiiB.Y);
        var travel = motionB.Grid.Travel - motionA.Grid.Travel;
        var turn = motionA.Grid.TurnSize * (motionA.Offset.Length() + (radiiA.X == radiiA.Y ? 0f : reachA)) +
            motionB.Grid.TurnSize * (motionB.Offset.Length() + (radiiB.X == radiiB.Y ? 0f : reachB));
        var speed = travel.Length() + turn;
        var reach = reachA + reachB + speed;
        if (speed <= RimTolerance || Vector2.DistanceSquared(motionB.CenterAt(0f), motionA.CenterAt(0f)) > reach * reach)
            return;

        ReadContact(0f, out var axis, out var gap, out var start);
        if (start >= 1f - RimTolerance && Vector2.Dot(travel, new Vector2(axis.X, axis.Y)) >= turn)
            return;

        var fraction = 0f;
        if (start < 1f - RimTolerance)
        {
            // Activation can start with overlapping shields; don't pull them together when backing out.
            ReadContact(1f, out _, out _, out var end);
            if (end >= start)
                return;
        }
        else
        {
            for (var i = 0; i < 96; i++)
            {
                var step = gap / speed;
                if (step >= 1f - fraction)
                    return;
                if (gap <= RimTolerance)
                    break;

                fraction += step * 0.9f;
                ReadContact(fraction, out axis, out gap, out _);
            }
        }

        var normal = new Vector2(axis.X, axis.Y);
        if (normal.LengthSquared() <= RimTolerance)
            return;
        normal = Vector2.Normalize(normal);

        var shapeA = new ShipShieldContactShape(radiiA, motionA.RotationAt(fraction));
        var shapeB = new ShipShieldContactShape(radiiB, motionB.RotationAt(fraction));
        var pointA = shapeA.Surface(axis);
        var pointB = shapeB.Surface(-axis);
        var leverA = GetLever(motionA, pointA, radiiA);
        var leverB = GetLever(motionB, pointB, radiiB);
        var closing = Vector2.Dot(_physics.GetMapLinearVelocity(ownerA, motionA.Body) -
            _physics.GetMapLinearVelocity(ownerB, motionB.Body), normal);
        if (closing > 0f)
        {
            var impulse = normal * (closing / mass);
            if (motionA.Body != null)
                _physics.ApplyLinearImpulse(ownerA, -impulse, body: motionA.Body);
            if (motionB.Body != null)
                _physics.ApplyLinearImpulse(ownerB, impulse, body: motionB.Body);
        }

        var carry = (motionA.Grid.Travel * (motionB.Body?.InvMass ?? 0f) +
            motionB.Grid.Travel * (motionA.Body?.InvMass ?? 0f)) * ((1f - fraction) / mass);
        carry = Vector2.Dot(carry, normal) * normal;
        Stop(ownerA, motionA, leverA);
        Stop(ownerB, motionB, -leverB);

        var pushing = Vector2.Dot(motionA.Start.Thrust - motionB.Start.Thrust, normal) +
            motionA.Start.TurnDrive * leverA - motionB.Start.TurnDrive * leverB > 0.01f;
        if (pushing || closing > 0.01f || turn > 0.001f)
        {
            var hitA = new ShipShieldSurfaceContactEvent(pointA, Transform(a).MapID, pushing, ownerB);
            var hitB = new ShipShieldSurfaceContactEvent(pointB, Transform(b).MapID, pushing, ownerA);
            OnSurfaceContact(a, ref hitA);
            OnSurfaceContact(b, ref hitB);
        }

        void ReadContact(float t, out Vector3 face, out float separation, out float measure)
        {
            var shapeA = new ShipShieldContactShape(radiiA, motionA.RotationAt(t));
            var shapeB = new ShipShieldContactShape(radiiB, motionB.RotationAt(t));
            var delta = new Vector3(motionB.CenterAt(t) - motionA.CenterAt(t), dz);
            measure = shapeA.Measure(shapeB, delta, out face);
            separation = Vector3.Dot(delta, face) - shapeA.Support(face) - shapeB.Support(face);
        }

        float GetLever(ShellMotion motion, Vector3 point, Vector3 radii)
        {
            var surface = motion.RotationAt(fraction).RotateVec(new Vector2(point.X * radii.X, point.Y * radii.Y));
            var arm = motion.CenterAt(fraction) + surface - motion.Grid.At(fraction).Center;
            return Vector2Helpers.Cross(arm, normal);
        }

        void Stop(EntityUid owner, ShellMotion motion, float lever)
        {
            if (motion.Body == null || motion.Body.InvMass <= 0f)
                return;

            if (motion.Body.AngularVelocity * lever > RimTolerance)
                _physics.SetAngularVelocity(owner, 0f, body: motion.Body);
            var pose = motion.Grid.At(fraction);
            if (motion.Offset.LengthSquared() <= RimTolerance && motion.Grid.TurnSize > 0f && turn == 0f)
                pose = pose with { Rotation = motion.Grid.At(1f).Rotation };
            // Tangential travel stays on the safe side of the contact plane.
            var travel = motion.Grid.Travel;
            var slide = (travel - Vector2.Dot(travel, normal) * normal) * (1f - fraction);
            RestorePose(owner, Transform(owner), motion.Body, pose with { Center = pose.Center + carry + slide });
        }
    }

    private ShellMotion ReadShellMotion(EntityUid shield, EntityUid owner)
    {
        var (center, rotation) = _transform.GetWorldPositionRotation(shield);
        if (_gridStarts.TryGetValue(owner, out var start) && TryComp(owner, out PhysicsComponent? body) &&
            Transform(owner).ParentUid == start.Parent)
        {
            var end = ReadPose(Transform(owner), body);
            return new ShellMotion(new GridMotion(start.Pose, end), (-end.Rotation).RotateVec(center - end.Center),
                rotation - end.Rotation, body, start);
        }

        var pose = new GridPose(center, rotation, Vector2.Zero);
        return new ShellMotion(new GridMotion(pose, pose), Vector2.Zero, Angle.Zero, null, default);
    }

    private readonly record struct ShellMotion(GridMotion Grid, Vector2 Offset, Angle AngleOffset, PhysicsComponent? Body, GridStart Start)
    {
        public Vector2 CenterAt(float t)
        {
            var pose = Grid.At(t);
            return pose.Center + pose.Rotation.RotateVec(Offset);
        }

        public Angle RotationAt(float t) => Grid.At(t).Rotation + AngleOffset;
    }
}
