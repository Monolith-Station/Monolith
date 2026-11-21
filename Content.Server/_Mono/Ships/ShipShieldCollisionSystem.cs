using System.Numerics;
using Content.Server.Emp;
using Content.Server.Physics.Controllers;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Mono.Ships;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Robust.Server.GameStates;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Ships;

public sealed partial class ShipShieldCollisionSystem : EntitySystem
{
    private const int Sides = 48;
    private const int MaxRecentImpacts = 32;
    private static readonly TimeSpan PressureGrace = ShipShieldProjectorComponent.PressureGrace;
    private static readonly SoundSpecifier ProjectileImpactSound = new SoundPathSpecifier("/Audio/_Mono/Effects/Impacts/shp_hit_shield.ogg");

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private EmpSystem _emp = default!;
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedJointSystem _joints = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PvsOverrideSystem _pvs = default!;
    [Dependency] private SharedShipShieldShellSystem _containment = default!;
    [Dependency] private ShipShieldProjectorSystem _projectors = default!;
    [Dependency] private CESharedZLevelsSystem _levels = default!;
    [Dependency] private SharedMapSystem _maps = default!;

    private readonly Dictionary<EntityUid, Shell> _shells = new();
    private readonly HashSet<EntityUid> _visibleProjectors = new();
    private readonly Dictionary<(EntityUid Host, string Fixture), EntityUid> _owners = new();

    public bool HasShells => _shells.Count != 0;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(SharedShipShieldShellSystem));
        UpdatesBefore.Add(typeof(SharedPhysicsSystem));
        SubscribeLocalEvent<ShipShieldProjectorComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ShipShieldProjectorComponent, ShipShieldSurfaceContactEvent>(OnSurfaceContact);
        SubscribeLocalEvent<ShipShieldProjectorComponent, ShipShieldHitscanImpactEvent>(OnHitscanImpact);
        SubscribeLocalEvent<PhysicsComponent, StartCollideEvent>(OnContact);
        SubscribeLocalEvent<PhysicsComponent, EndCollideEvent>(OnEndContact);
        SubscribeLocalEvent<PhysicsUpdateBeforeSolveEvent>(OnBeforeSolve, after: new[] { typeof(MoverController) });
        SubscribeLocalEvent<PhysicsUpdateAfterSolveEvent>(OnAfterSolve);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ShipShieldProjectorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var shield, out var xform))
        {
            var impacts = shield.Impacts;
            if (impacts.Count > 0 && _timing.CurTime - impacts[^1].At > ShipShieldProjectorComponent.ImpactHistoryLifetime)
            {
                impacts.Clear();
                Dirty(uid, shield);
            }

            var shape = shield.Shield.Radii;
            var radii = new Vector2(shape.X, shape.Y);
            var hasField = xform.MapID != MapId.Nullspace && shape is { X: > 0.15f, Y: > 0.15f, Z: > 0f };
            // Clients near the rim need the restart deadline even after the physical shell is gone.
            if (hasField && shield.State != ShipShieldShellState.Off)
            {
                if (_visibleProjectors.Add(uid))
                    _pvs.AddGlobalOverride(uid);
            }
            else if (_visibleProjectors.Remove(uid))
                _pvs.RemoveGlobalOverride(uid);

            if (!hasField || !_containment.IsSolid(shield))
            {
                RemoveShell(uid);
                continue;
            }

            var grid = xform.GridUid != xform.MapUid ? xform.GridUid : null;
            var pose = grid is { } gridUid ? _transform.GetRelativePositionRotation(xform, gridUid) : (Vector2.Zero, Angle.Zero);
            var usesAltitude = _containment.GetStack(xform.MapUid) != null;
            var welded = !usesAltitude && TryComp(grid, out PhysicsComponent? gridBody) && gridBody.BodyType == BodyType.Dynamic;
            if (!_shells.TryGetValue(uid, out var shell) || shell.Grid != grid || shell.Pose != pose ||
                shell.Radii != radii || shell.Map != xform.MapID || shell.Welded != welded ||
                shell.UsesAltitude != usesAltitude || TerminatingOrDeleted(shell.Host))
            {
                RemoveShell(uid);
                shell = CreateShell(uid, xform, grid, pose, radii, welded, usesAltitude);
            }

            if (shell.Host != uid && !welded)
            {
                var (position, rotation) = _transform.GetWorldPositionRotation(xform);
                var hostTransform = Transform(shell.Host);
                if (hostTransform.LocalPosition != position)
                    _transform.SetCoordinates(shell.Host, new EntityCoordinates(xform.MapUid!.Value, position));
                if (hostTransform.LocalRotation != rotation)
                    _transform.SetWorldRotation(shell.Host, rotation);
            }

            foreach (var (key, point) in shell.Contacts)
            {
                if (_containment.IsPushing((uid, shield), key.Other, point))
                {
                    var speed = GetProxyClosingSpeed(uid, shell, key.Other, point);
                    var contact = new ShipShieldSurfaceContactEvent(point, xform.MapID, true, key.Other, speed);
                    _containment.ApplyBodyContact((uid, shield), ref contact);
                }
            }

            if (shell.PressureBursts.Count != 0)
            {
                foreach (var source in shell.PressureBursts.Keys)
                    if (_timing.CurTime - shell.PressureBursts[source] > PressureGrace)
                        shell.PressureBursts.Remove(source);
            }

        }
    }

    private Shell CreateShell(EntityUid uid, TransformComponent xform, EntityUid? grid,
        (Vector2 Position, Angle Rotation) pose, Vector2 radii, bool welded, bool usesAltitude)
    {
        if (usesAltitude)
        {
            var analytic = new Shell(uid, grid, xform.MapID, pose, radii, false, true);
            _shells.Add(uid, analytic);
            return analytic;
        }

        var host = uid;
        if (grid != null)
        {
            // Grid collision only considers tile fixtures. A map-parented body can contact
            // other grids outside those bounds, and the weld transfers the load to the ship.
            var (position, rotation) = _transform.GetWorldPositionRotation(xform);
            host = EntityManager.SpawnEntity("MonoShipShieldCollider", new EntityCoordinates(xform.MapUid!.Value, position));
            var hostTransform = Transform(host);
            hostTransform.GridTraversal = false;
            _transform.SetCoordinates(host, new EntityCoordinates(xform.MapUid.Value, position));
            _transform.SetWorldRotation(host, rotation);
            _pvs.AddGlobalOverride(host);
        }

        var collider = EnsureComp<ShipShieldColliderComponent>(host);
        collider.Grid = grid;
        collider.Shield = uid;
        Dirty(host, collider);

        var body = EnsureComp<PhysicsComponent>(host);
        var fixtures = EnsureComp<FixturesComponent>(host);
        var shell = new Shell(host, grid, xform.MapID, pose, radii, welded, false);
        _shells.Add(uid, shell);

        var inner = radii - new Vector2(0.12f);
        var vertices = new Vector2[4];
        for (var i = 0; i < Sides; i++)
        {
            var a = new Angle(i * MathF.Tau / Sides).ToVec();
            var b = new Angle((i + 1) % Sides * MathF.Tau / Sides).ToVec();
            vertices[0] = a * radii;
            vertices[1] = b * radii;
            vertices[2] = b * inner;
            vertices[3] = a * inner;
            var shape = new PolygonShape();
            if (!shape.Set(vertices, vertices.Length))
                continue;

            var id = $"{ShipShieldProjectorComponent.FixturePrefix}{uid.Id}-{i}";
            if (_fixtures.TryCreateFixture(host, shape, id, density: welded ? 0.1f : 0f, friction: 0f,
                    collisionLayer: (int) SharedShipShieldShellSystem.ShellCollisionLayer,
                    collisionMask: (int) SharedShipShieldShellSystem.ShellCollisionMask, updates: false, manager: fixtures, body: body))
            {
                shell.Fixtures.Add(id);
                _owners.Add((host, id), uid);
            }
        }

        _fixtures.FixtureUpdate(host, manager: fixtures, body: body);
        _physics.SetCanCollide(host, true, manager: fixtures, body: body);
        if (welded && grid is { } movingGrid)
        {
            _physics.SetBodyType(host, BodyType.Dynamic, manager: fixtures, body: body);
            var gridBody = Comp<PhysicsComponent>(movingGrid);
            _physics.SetLinearVelocity(host, _physics.GetMapLinearVelocity(movingGrid, gridBody), body: body);
            _physics.SetAngularVelocity(host, gridBody.AngularVelocity, body: body);
            var joint = _joints.CreateWeldJoint(movingGrid, host);
            joint.LocalAnchorA = pose.Position;
            joint.ReferenceAngle = (float) pose.Rotation.Theta;
            joint.CollideConnected = false;
        }

        return shell;
    }

    private void OnShutdown(Entity<ShipShieldProjectorComponent> ent, ref ComponentShutdown args)
    {
        if (_visibleProjectors.Remove(ent.Owner))
            _pvs.RemoveGlobalOverride(ent.Owner);
        RemoveShell(ent.Owner);
    }

    private void RemoveShell(EntityUid uid)
    {
        if (!_shells.Remove(uid, out var shell))
            return;

        var host = shell.Host;
        foreach (var id in shell.Fixtures)
        {
            _owners.Remove((host, id));
            if (host == uid && !TerminatingOrDeleted(host))
                _fixtures.DestroyFixture(host, id, updates: false);
        }

        if (TerminatingOrDeleted(host))
            return;

        if (host != uid)
        {
            _physics.SetCanCollide(host, false);
            QueueDel(host);
        }
        else if (shell.Fixtures.Count != 0)
        {
            _fixtures.FixtureUpdate(host);
            RemComp<ShipShieldColliderComponent>(host);
        }
    }

    private void OnContact(Entity<PhysicsComponent> ent, ref StartCollideEvent args)
    {
        if (!args.OtherFixture.Hard || args.PointCount == 0 ||
            !_owners.TryGetValue((ent.Owner, args.OurFixtureId), out var uid) ||
            !TryComp(uid, out ShipShieldProjectorComponent? shield) ||
            HasComp<ProjectileComponent>(args.OtherEntity))
            return;

        var shell = _shells[uid];
        if (Transform(args.OtherEntity).Anchored || args.OtherBody.InvMass <= 0f)
            return;

        var worldPoint = args.WorldPoints[0];
        var point = _containment.GetImpactOrigin((uid, shield), worldPoint);
        var map = Transform(uid).MapID;
        var speed = GetProxyClosingSpeed(uid, shell, args.OtherEntity, point);
        var contact = new ShipShieldSurfaceContactEvent(point, map, _containment.IsPushing((uid, shield), args.OtherEntity, point),
            args.OtherEntity, speed);
        _containment.ApplyBodyContact((uid, shield), ref contact);

        shell.Contacts[(args.OurFixtureId, args.OtherEntity, args.OtherFixtureId)] = point;
    }

    private float GetProxyClosingSpeed(EntityUid emitter, Shell shell, EntityUid source, Vector3 point)
    {
        var (center, rotation) = _transform.GetWorldPositionRotation(emitter);
        var radial = new Vector2(point.X, point.Y);
        if (radial.LengthSquared() < 0.0001f)
            return 0f;

        var local = (-rotation).RotateVec(_transform.GetWorldPosition(source) - center) / shell.Radii;
        var normal = rotation.RotateVec(Vector2.Normalize(radial / shell.Radii));
        if (local.LengthSquared() < 1f)
            normal = -normal;

        var surface = center + rotation.RotateVec(radial * shell.Radii);
        var velocity = _physics.GetMapLinearVelocity(source) - _physics.GetMapLinearVelocity(shell.Host) -
            Vector2Helpers.Cross(_physics.GetMapAngularVelocity(shell.Host), surface - _transform.GetWorldPosition(shell.Host));
        return -Vector2.Dot(velocity, normal);
    }

    private void OnEndContact(Entity<PhysicsComponent> ent, ref EndCollideEvent args)
    {
        if (_owners.TryGetValue((ent.Owner, args.OurFixtureId), out var uid))
            _shells[uid].Contacts.Remove((args.OurFixtureId, args.OtherEntity, args.OtherFixtureId));
    }

    public bool TryFindProjectileHit(EntityUid projectile, MapCoordinates start, Vector2 direction, float maxDistance,
        out EntityUid shield, out float distance)
    {
        var hit = _containment.TryFindProjectileHit(projectile, start.Position, direction * maxDistance, out shield, out var fraction);
        distance = fraction * maxDistance;
        return hit;
    }

    public void ConsumeProjectile(Entity<ProjectileComponent> projectile, EntityUid shieldUid, Vector2 worldPoint,
        Vector3? surfacePoint = null)
    {
        if (projectile.Comp.ProjectileSpent || !_shells.ContainsKey(shieldUid) ||
            !TryComp(shieldUid, out ShipShieldProjectorComponent? shield) || !_containment.IsSolid(shield))
            return;

        var origin = surfacePoint ?? _containment.GetImpactOrigin((shieldUid, shield), worldPoint,
            _levels.GetAbsoluteAltitude(projectile));
        var map = Transform(projectile).MapID;
        var empEnergy = GetEmpEnergy(projectile);
        var power = SharedShipShieldShellSystem.GetImpactPower((float) projectile.Comp.Damage.GetTotal() + shield.GetEmpDamage(empEnergy));
        projectile.Comp.ProjectileSpent = true;
        SetImpact((shieldUid, shield), origin, map, power);
        _projectors.AbsorbHit((shieldUid, shield), projectile.Comp.Damage, empEnergy);
        var coordinates = _transform.ToCoordinates(new MapCoordinates(worldPoint, map));
        _audio.PlayPvs(ProjectileImpactSound, coordinates,
            AudioParams.Default.WithVolume(-5f + 12f * MathF.Log10(power)).WithVariation(0.05f));
        QueueDel(projectile);
    }

    public bool TryBlockProjectileCrossing(Entity<ProjectileComponent> projectile, MapCoordinates destination)
    {
        if (!HasShells || !_maps.TryGetMap(destination.MapId, out var map))
            return false;

        var source = Transform(projectile).MapUid;
        if (_containment.GetStack(source) is not { } stack || _containment.GetStack(map) != stack ||
            !_containment.TryFindVerticalContact(projectile, source, destination.Position, _levels.GetAbsoluteAltitude(projectile),
                _levels.GetAbsoluteAltitude(map.Value), out var hit, out var surfacePoint, out _))
            return false;

        ConsumeProjectile(projectile, hit, destination.Position, surfacePoint);
        return projectile.Comp.ProjectileSpent;
    }

    public void ConsumeProjectile(Entity<ProjectileComponent> projectile, ref StartCollideEvent args)
    {
        if (_owners.TryGetValue((args.OtherEntity, args.OtherFixtureId), out var shield))
        {
            var point = args.PointCount > 0 ? args.WorldPoints[0] : _transform.GetWorldPosition(projectile.Owner);
            ConsumeProjectile(projectile, shield, point);
        }
    }

    private void SetImpact(Entity<ShipShieldProjectorComponent> shield, Vector3 point, MapId map,
        float power = ShipShieldProjectorComponent.ContactImpactPower, EntityUid? source = null)
    {
        var now = _timing.CurTime;
        var impacts = shield.Comp.Impacts;
        impacts.RemoveAll(impact => now - impact.At > ShipShieldProjectorComponent.ImpactHistoryLifetime);
        if (impacts.Count >= MaxRecentImpacts)
            impacts.RemoveAt(0);

        impacts.Add(new ShipShieldImpact(++shield.Comp.NextImpactId, point, map, now, power, GetNetEntity(source)));
        Dirty(shield);
    }

    private void OnHitscanImpact(Entity<ShipShieldProjectorComponent> shield, ref ShipShieldHitscanImpactEvent args)
    {
        var empEnergy = GetEmpEnergy(args.Shot);
        var power = SharedShipShieldShellSystem.GetImpactPower((float) args.Damage.GetTotal() + shield.Comp.GetEmpDamage(empEnergy));
        SetImpact(shield, _containment.GetImpactOrigin(shield, args.Point, args.Altitude), args.Map, power);
        _projectors.AbsorbHit(shield, args.Damage, empEnergy);
    }

    private float GetEmpEnergy(EntityUid shot)
    {
        var energy = _emp.GetTriggerEnergy(shot);
        if (TryComp(shot, out HitscanSpawnEntityComponent? impact))
            energy += _emp.GetTriggerEnergy(impact.SpawnedEntity);

        return energy;
    }

    private void OnSurfaceContact(Entity<ShipShieldProjectorComponent> shield, ref ShipShieldSurfaceContactEvent args)
    {
        if (!_shells.TryGetValue(shield.Owner, out var shell))
            return;

        if (args.BodyImpactSpeed is { } speed && args.Source is { } source)
        {
            var damage = _containment.GetBodyImpactDamage(shield.Comp, source, speed);
            SetImpact(shield, args.Point, args.Map, SharedShipShieldShellSystem.GetBodyImpactPower(damage), source);
            shell.LastPassiveContact = _timing.CurTime;
            return;
        }

        if (args.Pushing)
            SetPressure(shield, shell, args.Point, args.Map, args.Source);
        else if (!shell.PressureBursts.ContainsKey(args.Source ?? EntityUid.Invalid) &&
                 (shell.LastPassiveContact is not { } last || _timing.CurTime - last > PressureGrace))
            SetImpact(shield, args.Point, args.Map, source: args.Source);

        shell.LastPassiveContact = _timing.CurTime;
    }

    private void SetPressure(Entity<ShipShieldProjectorComponent> shield, Shell shell, Vector3 point, MapId map, EntityUid? source)
    {
        var now = _timing.CurTime;
        var key = source ?? EntityUid.Invalid;
        if (shell.PressureBursts.TryGetValue(key, out var last) && now - last < _timing.TickPeriod * 3)
            return;

        shell.PressureBursts[key] = now;
        SetImpact(shield, point, map, source: source);
    }

    private sealed class Shell(EntityUid host, EntityUid? grid, MapId map, (Vector2 Position, Angle Rotation) pose,
        Vector2 radii, bool welded, bool usesAltitude)
    {
        public readonly EntityUid Host = host;
        public readonly EntityUid? Grid = grid;
        public readonly MapId Map = map;
        public readonly bool Welded = welded;
        public readonly bool UsesAltitude = usesAltitude;
        public readonly (Vector2 Position, Angle Rotation) Pose = pose;
        public readonly Vector2 Radii = radii;
        public readonly List<string> Fixtures = new(Sides);
        public readonly Dictionary<(string Fixture, EntityUid Other, string OtherFixture), Vector3> Contacts = new();
        public readonly Dictionary<EntityUid, TimeSpan> PressureBursts = new();
        public TimeSpan? LastPassiveContact;
    }
}
