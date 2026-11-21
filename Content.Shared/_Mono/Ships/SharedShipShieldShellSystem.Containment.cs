using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Mono.Ships;

public sealed partial class SharedShipShieldShellSystem
{
    public const CollisionGroup ShellCollisionLayer =
        CollisionGroup.LowImpassable | CollisionGroup.MidImpassable | CollisionGroup.HighImpassable;
    public const CollisionGroup ShellCollisionMask =
        CollisionGroup.WallLayer | CollisionGroup.DoorPassable | CollisionGroup.MapGrid;

    [Dependency] private CESharedZLevelsSystem _levels = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private INetManager _net = default!;

    private void InitializeContainment()
    {
        SubscribeLocalEvent<PhysicsUpdateBeforeSolveEvent>(
            OnBeforeSolve, after: new[] { typeof(SharedMoverController) });
    }

    public EntityUid? GetStack(EntityUid? map)
    {
        if (TryComp(map, out CEZTransitMapComponent? transit))
            map = transit.LowerMap;

        return TryComp(map, out CEZMapComponent? level) ? level.NetworkUid : null;
    }

    private bool BlocksBody(ShipShieldShellSettings settings, EntityUid body) =>
        HasComp<ProjectileComponent>(body) ||
        settings.Blocks(HasComp<MobStateComponent>(body) ? ShipShieldCollision.Mobs : ShipShieldCollision.Objects);

    public bool TryGetSlice(Entity<ShipShieldProjectorComponent> shield, EntityUid? map, float altitude, out Vector2 radii)
    {
        var shape = shield.Comp.Shield.Radii;
        radii = new Vector2(shape.X, shape.Y);
        if (shape is not { X: > 0f, Y: > 0f, Z: > 0f })
            return false;

        var sourceMap = Transform(shield).MapUid;
        if (GetStack(sourceMap) is not { } stack)
            return sourceMap == map;

        if (GetStack(map) != stack)
            return false;

        var z = GetSurfaceHeight(shield, altitude);
        if (MathF.Abs(z) >= 1f)
            return false;

        radii *= MathF.Sqrt(1f - z * z);
        return true;
    }

    private void OnBeforeSolve(ref PhysicsUpdateBeforeSolveEvent args)
    {
        var shells = EntityQueryEnumerator<ShipShieldProjectorComponent, TransformComponent>();
        while (shells.MoveNext(out var uid, out var shield, out var xform))
        {
            if (xform.MapUid == null || !IsSolid(shield))
                continue;

            var stack = GetStack(xform.MapUid);
            var inverse = _transform.GetInvWorldMatrix(xform);
            var (center, rotation) = _transform.GetWorldPositionRotation(xform);
            var shellVelocity = _physics.GetMapLinearVelocity(uid);
            var shellAngularVelocity = _physics.GetMapAngularVelocity(uid);
            var bodies = EntityQueryEnumerator<PhysicsComponent, TransformComponent>();
            while (bodies.MoveNext(out var bodyUid, out var body, out var bodyXform))
            {
                if (bodyUid == uid || !body.Awake || !body.CanCollide || !body.Hard || body.InvMass <= 0f || bodyXform.Anchored ||
                    (_net.IsClient && !body.Predict) ||
                    (stack == null && (bodyXform.MapUid != xform.MapUid || body.BodyType != BodyType.KinematicController)) ||
                    HasComp<MapGridComponent>(bodyUid) || HasComp<ShipShieldColliderComponent>(bodyUid) ||
                    HasComp<ProjectileComponent>(bodyUid) || !BlocksBody(shield.Shield, bodyUid) || !CanHitShell(bodyUid) ||
                    !TryGetSlice((uid, shield), bodyXform.MapUid, _levels.GetAbsoluteAltitude(bodyUid), out var radii))
                    continue;

                var position = _transform.GetWorldPosition(bodyXform);
                var velocity = _physics.GetMapLinearVelocity(bodyUid, body, bodyXform)
                    - shellVelocity - Vector2Helpers.Cross(shellAngularVelocity, position - center);
                var local = Vector2.Transform(position, inverse);
                var reach = MathF.Max(radii.X, radii.Y) + velocity.Length() * args.DeltaTime + 1f;
                if (local.LengthSquared() > reach * reach)
                    continue;

                var inside = (local / radii).LengthSquared() < 1f;
                var bounds = _physics.GetHardAABB(bodyUid, body: body, xform: bodyXform);
                var padding = 0.5f * MathF.Min(bounds.Width, bounds.Height);
                var boundary = inside ? Vector2.Max(radii - new Vector2(padding), new Vector2(0.01f)) : radii + new Vector2(padding);
                var point = local / boundary;
                var delta = Vector2.TransformNormal(velocity * args.DeltaTime, inverse) / boundary;
                var normal = point;
                var fraction = 0f;
                if ((point.LengthSquared() < 1f) == inside)
                {
                    if (!TryIntersect(point, delta, inside, out fraction))
                        continue;
                    normal += delta * fraction;
                }

                if (normal.LengthSquared() <= 0.0001f)
                    continue;

                var worldNormal = rotation.RotateVec(Vector2.Normalize(normal / boundary)) * (inside ? -1f : 1f);
                var speed = Vector2.Dot(velocity, worldNormal);
                if (speed >= -0.001f)
                    continue;

                // The physics solver stores linear velocity in map axes even for grid children.
                var correction = worldNormal * (speed * (1f - fraction));
                _physics.SetLinearVelocity(bodyUid, body.LinearVelocity - correction, body: body);
                var impact = Vector2.Normalize(normal) * radii / new Vector2(shield.Shield.Radii.X, shield.Shield.Radii.Y);
                var height = stack == null ? 0f : GetSurfaceHeight((uid, shield), _levels.GetAbsoluteAltitude(bodyUid));
                ReportContact(uid, new Vector3(impact, height), bodyXform.MapID, HasMovementIntent(bodyUid), bodyUid, -speed);
                if (!IsSolid(shield))
                    break;
            }
        }
    }

    public float ClampVertical(EntityUid mover, EntityUid? map, float from, float to, bool pushing, bool gridFootprint = false,
        IReadOnlySet<EntityUid>? ignoreGrids = null, float verticalSpeed = 0f)
    {
        if (gridFootprint ? !TryComp(mover, out PhysicsComponent? body) || !body.CanCollide : !CanHitShell(mover))
            return to;

        var position = _transform.GetWorldPosition(mover);
        var hitHull = TryFindVerticalContact(mover, map, position, from, to, out var hit, out var point, out var altitude,
            gridFootprint, ignoreGrids);
        if (gridFootprint && ClampVerticalShells(mover, map, from, ref altitude, pushing, ignoreGrids))
            return altitude;

        if (hitHull)
            ReportContact(hit, point, Transform(mover).MapID, pushing, mover,
                gridFootprint ? null : MathF.Abs(verticalSpeed));

        return altitude;
    }

    private bool ClampVerticalShells(EntityUid mover, EntityUid? map, float from, ref float to, bool pushing,
        IReadOnlySet<EntityUid>? ignoreGrids)
    {
        if (from == to || GetStack(map) is not { } stack)
            return false;

        EntityUid source = default;
        EntityUid target = default;
        Vector3 sourcePoint = default;
        Vector3 targetPoint = default;
        var moving = EntityQueryEnumerator<ShipShieldProjectorComponent, TransformComponent>();
        while (moving.MoveNext(out var uid, out var shield, out var xform))
        {
            var radii = shield.Shield.Radii;
            if (xform.GridUid != mover || !shield.Shield.Blocks(ShipShieldCollision.Shields) || !IsSolid(shield) ||
                radii is not { X: > 0.15f, Y: > 0.15f, Z: > 0f })
                continue;

            var (center, rotation) = _transform.GetWorldPositionRotation(uid);
            var shape = new ShipShieldContactShape(radii, rotation);
            var offset = _levels.GetAbsoluteAltitude(uid) + shield.Shield.CenterHeight - _levels.GetAbsoluteAltitude(mover);
            var others = EntityQueryEnumerator<ShipShieldProjectorComponent, TransformComponent>();
            while (others.MoveNext(out var other, out var otherShield, out var otherXform))
            {
                var otherRadii = otherShield.Shield.Radii;
                if ((otherXform.GridUid is { } host && (host == mover || ignoreGrids?.Contains(host) == true)) ||
                    !otherShield.Shield.Blocks(ShipShieldCollision.Shields) || !IsSolid(otherShield) ||
                    GetStack(otherXform.MapUid) != stack ||
                    otherRadii is not { X: > 0.15f, Y: > 0.15f, Z: > 0f })
                    continue;

                var (otherCenter, otherRotation) = _transform.GetWorldPositionRotation(other);
                var xy = otherCenter - center;
                var reach = MathF.Max(radii.X, radii.Y) + MathF.Max(otherRadii.X, otherRadii.Y);
                if (xy.LengthSquared() >= reach * reach)
                    continue;

                var align = _levels.GetAbsoluteAltitude(other) + otherShield.Shield.CenterHeight - offset;
                var near = Math.Clamp(align, MathF.Min(from, to), MathF.Max(from, to));
                if (near == from || MathF.Abs(align - near) >= radii.Z + otherRadii.Z)
                    continue;

                var otherShape = new ShipShieldContactShape(otherRadii, otherRotation);
                if (shape.Measure(otherShape, new Vector3(xy, align - near), out _) >= 1f)
                    continue;

                var safe = from;
                var blocked = near;
                for (var i = 0; i < 18; i++)
                {
                    var middle = (safe + blocked) * 0.5f;
                    if (shape.Measure(otherShape, new Vector3(xy, align - middle), out _) >= 1f)
                        safe = middle;
                    else
                        blocked = middle;
                }

                to = safe;
                shape.Measure(otherShape, new Vector3(xy, align - safe), out var axis);
                source = uid;
                target = other;
                sourcePoint = shape.Surface(axis);
                targetPoint = otherShape.Surface(-axis);
            }
        }

        if (!target.IsValid())
            return false;

        var mapId = Transform(mover).MapID;
        ReportContact(target, targetPoint, mapId, pushing, mover);
        ReportContact(source, sourcePoint, mapId, pushing, Transform(target).GridUid ?? target);
        return true;
    }

    public bool TryFindVerticalContact(EntityUid mover, EntityUid? map, Vector2 position, float from, float to,
        out EntityUid hit, out Vector3 hitPoint, out float target, bool gridFootprint = false,
        IReadOnlySet<EntityUid>? ignoreGrids = null)
    {
        hit = EntityUid.Invalid;
        hitPoint = Vector3.Zero;
        target = to;
        if (from == to || GetStack(map) is not { } stack)
            return false;

        var direction = MathF.Sign(to - from);
        MapGridComponent? grid = null;
        if (gridFootprint && !TryComp(mover, out grid))
            return false;

        var gridWorld = gridFootprint ? _transform.GetWorldMatrix(mover) : Matrix3x2.Identity;
        var vertices = gridFootprint ? new Vector2[4] : null;
        var nearestHit = to;
        var surfaceHit = Vector3.Zero;
        var query = EntityQueryEnumerator<ShipShieldProjectorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var shield, out var xform))
        {
            var radii = shield.Shield.Radii;
            if (uid == mover || (xform.GridUid is { } host && (host == mover || ignoreGrids?.Contains(host) == true)) ||
                !IsSolid(shield) || GetStack(xform.MapUid) != stack || radii is not { X: > 0f, Y: > 0f, Z: > 0f } ||
                (gridFootprint ? !shield.Shield.Blocks(ShipShieldCollision.Grids) : !BlocksBody(shield.Shield, mover)))
                continue;

            var center = _levels.GetAbsoluteAltitude(uid) + shield.Shield.CenterHeight;
            var inverse = _transform.GetInvWorldMatrix(xform);
            var xyRadii = new Vector2(radii.X, radii.Y);
            var previous = nearestHit;
            if (grid != null)
            {
                var toShell = gridWorld * inverse * Matrix3x2.CreateScale(Vector2.One / xyRadii);
                var bounds = Box2.CenteredAround(_transform.GetWorldPosition(xform.Owner),
                    new Vector2(2f * MathF.Max(radii.X, radii.Y)));
                foreach (var tile in _map.GetTilesIntersecting(mover, grid, bounds))
                {
                    WriteGridTileVertices(tile.GridIndices, grid.TileSize, vertices!);
                    GetFootprintExtrema(vertices, toShell, out var closest, out var farthest);
                    CheckFootprint(center, radii.Z, closest, farthest);
                }
            }
            else
            {
                var local = Vector2.Transform(position, inverse) / xyRadii;
                CheckFootprint(center, radii.Z, local, local);
            }

            if (nearestHit != previous)
                hit = uid;
        }

        target = nearestHit;
        hitPoint = surfaceHit;
        return hit.IsValid();

        void CheckFootprint(float center, float radius, Vector2 closest, Vector2 farthest)
        {
            var near = closest.LengthSquared();
            if (near >= 1f)
                return;

            var outer = radius * MathF.Sqrt(1f - near);
            var far = farthest.LengthSquared();
            if (far >= 1f)
            {
                CheckBand(center - outer, center + outer, closest, closest);
                return;
            }

            var inner = radius * MathF.Sqrt(1f - far);
            CheckBand(center - outer, center - inner, closest, farthest);
            CheckBand(center + inner, center + outer, farthest, closest);

            void CheckBand(float low, float high, Vector2 lowerPoint, Vector2 upperPoint)
            {
                var boundary = direction > 0f ? low : high;
                if ((nearestHit - boundary) * direction <= 0f)
                    return;

                var distance = (boundary - from) * direction;
                if (distance < 0f)
                {
                    // Already intersecting (for example, the shield spun up here). Let the hull back out.
                    if (from <= low || from >= high || (from - (low + high) * 0.5f) * direction >= 0f)
                        return;
                }

                var clamped = distance <= 0.001f ? from : boundary - direction * 0.001f;
                if ((nearestHit - clamped) * direction <= 0f)
                    return;

                nearestHit = clamped;
                surfaceHit = new Vector3(direction > 0f ? lowerPoint : upperPoint, (boundary - center) / radius);
            }
        }
    }

    public static void WriteGridTileVertices(Vector2i indices, float size, Span<Vector2> vertices)
    {
        var corner = (Vector2) indices * size;
        vertices[0] = corner;
        vertices[1] = corner + new Vector2(size, 0f);
        vertices[2] = corner + new Vector2(size, size);
        vertices[3] = corner + new Vector2(0f, size);
    }

    public static Vector2[] GetFootprintVertices(Fixture fixture)
    {
        if (fixture.Shape is PolygonShape polygon)
            return polygon.Vertices;

        var box = fixture.Shape.ComputeAABB(new Robust.Shared.Physics.Transform(Vector2.Zero, 0f), 0);
        return new[] { box.BottomLeft, box.BottomRight, box.TopRight, box.TopLeft };
    }

    public static void GetFootprintExtrema(ReadOnlySpan<Vector2> vertices, Matrix3x2 transform,
        out Vector2 closest, out Vector2 farthest)
    {
        closest = farthest = Vector2.Transform(vertices[0], transform);
        var positive = false;
        var negative = false;
        var a = Vector2.Transform(vertices[^1], transform);
        foreach (var vertex in vertices)
        {
            var b = Vector2.Transform(vertex, transform);
            var edge = b - a;
            var cross = Vector2Helpers.Cross(a, b);
            positive |= cross > 0f;
            negative |= cross < 0f;
            var length = edge.LengthSquared();
            var point = length > 0f ? a + edge * Math.Clamp(-Vector2.Dot(a, edge) / length, 0f, 1f) : a;
            if (point.LengthSquared() < closest.LengthSquared())
                closest = point;
            if (b.LengthSquared() > farthest.LengthSquared())
                farthest = b;
            a = b;
        }

        if (!positive || !negative)
            closest = Vector2.Zero;
    }

    public static bool TryIntersect(Vector2 point, Vector2 delta, bool inside, out float fraction)
    {
        var a = delta.LengthSquared();
        var b = Vector2.Dot(point, delta);
        var c = point.LengthSquared() - 1f;
        var discriminant = b * b - a * c;
        fraction = a > 0f && discriminant >= 0f ? (-b + (inside ? 1f : -1f) * MathF.Sqrt(discriminant)) / a : -1f;
        return fraction is >= 0f and <= 1f;
    }

    public bool HasMovementIntent(EntityUid uid) =>
        TryComp(uid, out InputMoverComponent? mover) && mover.CanMove &&
        (mover.HeldMoveButtons & ~MoveButtons.Walk) != MoveButtons.None;

    public static bool MatchesShellFilter(Fixture fixture) =>
        ((CollisionGroup) fixture.CollisionLayer & ShellCollisionMask) != CollisionGroup.None ||
        ((CollisionGroup) fixture.CollisionMask & ShellCollisionLayer) != CollisionGroup.None;

    private static bool IsShellFixture(Fixture fixture) =>
        (CollisionGroup) fixture.CollisionLayer == ShellCollisionLayer &&
        (CollisionGroup) fixture.CollisionMask == ShellCollisionMask;

    private bool CanHitShell(EntityUid uid)
    {
        if (!TryComp(uid, out PhysicsComponent? body) || !body.CanCollide ||
            !TryComp(uid, out FixturesComponent? fixtures))
            return false;

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (fixture.Hard && MatchesShellFilter(fixture))
                return true;
        }

        return false;
    }

    public bool IsSolid(ShipShieldProjectorComponent shield) => shield.State == ShipShieldShellState.Active &&
        shield.AnimationPhase(_timing.CurTime) >= 1f;

    public bool TryFindProjectileHit(EntityUid projectile, Vector2 start, Vector2 step,
        out EntityUid shield, out float fraction) =>
        TryFindRayHit(Transform(projectile).MapUid, _levels.GetAbsoluteAltitude(projectile), start, step,
            out shield, out fraction);

    public bool TryFindRayHit(EntityUid? map, float altitude, Vector2 start, Vector2 step,
        out EntityUid shield, out float fraction)
    {
        shield = EntityUid.Invalid;
        fraction = 1f;
        var query = EntityQueryEnumerator<ShipShieldProjectorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var component, out var transform))
        {
            if (!IsSolid(component) || !TryGetSlice((uid, component), map, altitude, out var radii))
                continue;

            var inverse = _transform.GetInvWorldMatrix(transform);
            var point = Vector2.Transform(start, inverse) / radii;
            var delta = Vector2.TransformNormal(step, inverse) / radii;
            if (!TryIntersect(point, delta, point.LengthSquared() < 1f, out var hit) || hit > fraction)
                continue;

            shield = uid;
            fraction = hit;
        }

        return shield.IsValid();
    }

    public float GetSurfaceHeight(Entity<ShipShieldProjectorComponent> shield, float altitude) =>
        (altitude - _levels.GetAbsoluteAltitude(shield) - shield.Comp.Shield.CenterHeight) / shield.Comp.Shield.Radii.Z;

    public Vector3 GetImpactOrigin(Entity<ShipShieldProjectorComponent> shield, Vector2 worldPoint, float altitude = 0f)
    {
        var radii = shield.Comp.Shield.Radii;
        var point = Vector2.Transform(worldPoint, _transform.GetInvWorldMatrix(shield)) / new Vector2(radii.X, radii.Y);
        var z = GetStack(Transform(shield).MapUid) == null ? 0f : Math.Clamp(GetSurfaceHeight(shield, altitude), -1f, 1f);
        var length = point.Length();
        var edge = length > 0.0001f ? point * (MathF.Sqrt(1f - z * z) / length) : Vector2.Zero;
        return new Vector3(edge, z);
    }

    public static float GetImpactPower(float damage) => Math.Clamp(MathF.Sqrt(MathF.Max(0f, damage) / 20f), 0.30f, 2.0f);

    public float GetBodyImpactDamage(ShipShieldProjectorComponent shield, EntityUid source, float speed)
    {
        if (speed < 1f || !TryComp(source, out PhysicsComponent? body) || body.InvMass <= 0f)
            return 0f;

        return shield.ImpactDamageScale * speed * speed / body.InvMass;
    }

    public static float GetBodyImpactPower(float damage) =>
        Math.Clamp(ShipShieldProjectorComponent.ContactImpactPower + 0.45f * MathF.Log10(1f + MathF.Max(0f, damage) / 10f),
            ShipShieldProjectorComponent.ContactImpactPower, 2f);

    public bool IsPushing(Entity<ShipShieldProjectorComponent> shield, EntityUid other, Vector3 point)
    {
        if (!HasMovementIntent(other) || !TryComp(other, out InputMoverComponent? mover) || Transform(other).Anchored)
            return false;

        var inverse = _transform.GetInvWorldMatrix(shield);
        var radii = new Vector2(shield.Comp.Shield.Radii.X, shield.Comp.Shield.Radii.Y);
        var position = Vector2.Transform(_transform.GetWorldPosition(other), inverse) / radii;
        var radial = Vector2.Dot(Vector2.TransformNormal(mover.WishDir, inverse) / radii, new Vector2(point.X, point.Y));
        return position.LengthSquared() >= 1f ? radial < -0.01f : radial > 0.01f;
    }

    private void ReportContact(EntityUid uid, Vector3 point, MapId map, bool pushing, EntityUid source,
        float? bodyImpactSpeed = null)
    {
        var contact = new ShipShieldSurfaceContactEvent(point, map, pushing, source, bodyImpactSpeed);
        if (bodyImpactSpeed != null && TryComp(uid, out ShipShieldProjectorComponent? shield))
        {
            ApplyBodyContact((uid, shield), ref contact);
            return;
        }

        RaiseLocalEvent(uid, ref contact);
    }

    public void ApplyBodyContact(Entity<ShipShieldProjectorComponent> shield, ref ShipShieldSurfaceContactEvent contact)
    {
        if (!IsSolid(shield.Comp) || contact.Source is not { } source || contact.BodyImpactSpeed is not { } speed)
            return;

        var key = GetNetEntity(source);
        var contacts = shield.Comp.BodyContacts;
        var last = contacts.GetValueOrDefault(key);
        var now = _timing.CurTime;
        if (contact.Pushing)
        {
            if (now < last.PressureUntil)
                return;

            contacts[key] = last with { PressureUntil = now + _timing.TickPeriod * 3 };
        }
        else
        {
            if (speed <= 0.1f && now >= last.PassiveUntil)
                return;

            contacts[key] = last with { PassiveUntil = now + ShipShieldProjectorComponent.PressureGrace };
            Dirty(shield);
            if (now < last.PassiveUntil)
                return;
        }

        RaiseLocalEvent(shield.Owner, ref contact);
        var amount = GetBodyImpactDamage(shield.Comp, source, speed);
        if (amount >= 0.01f)
        {
            var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = amount } };
            var cost = (float) DamageSpecifier.ApplyModifierSet(damage, shield.Comp.DamageModifiers).GetTotal();
            if (cost > 0f)
                SpendCapacity(shield, cost);
        }

        Dirty(shield);
    }

    public bool SpendCapacity(Entity<ShipShieldProjectorComponent> shield, float damage, bool crash = false)
    {
        var comp = shield.Comp;
        var remaining = comp.Capacity - damage;
        comp.Damage = comp.MaxCapacity - Math.Clamp(remaining, 0f, comp.MaxCapacity);
        if (remaining > 0f || crash && remaining == 0f)
        {
            Dirty(shield);
            return false;
        }

        var now = _timing.CurTime;
        var cooldown = TimeSpan.FromSeconds(crash ? comp.CrashRespoolTime : comp.BreakRespoolTime);
        var shatterTime = comp.AnimationDuration(ShipShieldShellState.Shattering);
        var remainingStall = comp.RechargeStalledUntil > now ? comp.RechargeStalledUntil - now : TimeSpan.Zero;
        comp.RespoolUntil = now + (cooldown > shatterTime ? cooldown : shatterTime) + remainingStall;
        SetState(shield, ShipShieldShellState.Shattering);
        return true;
    }
}

[ByRefEvent]
public readonly record struct ShipShieldSurfaceContactEvent(Vector3 Point, MapId Map, bool Pushing,
    EntityUid? Source = null, float? BodyImpactSpeed = null);

[ByRefEvent]
public readonly record struct ShipShieldHitscanImpactEvent(Vector2 Point, MapId Map, float Altitude, EntityUid Shot, DamageSpecifier Damage);
