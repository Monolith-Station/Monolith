using System.Numerics;
using Content.Server._Mono.Projectiles.TargetGuided;
using Content.Shared._Mono.FireControl;
using Content.Shared.Projectiles;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server._Mono.Sensors;

public sealed partial class RadarGuidanceSystem : EntitySystem
{
    [Dependency] private RadarLockSystem _lock = default!;
    [Dependency] private TargetGuidedSystem _targetGuided = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    public override void Update(float frameTime)
    {
        var consoleQ = EntityQueryEnumerator<FireControlConsoleComponent>();
        while (consoleQ.MoveNext(out var consoleUid, out var console))
        {
            if (!console.RadarTrackEnabled)
                continue;
            if (!_lock.IsLockValid(consoleUid, console))
                continue;
            if (Transform(consoleUid).GridUid is not { } ourGrid)
                continue;
            if (console.LockedTarget is not { } target)
                continue;
            if (console.LockQuality < 0.2f)
                continue;
            if (!_lock.HasIllumination(ourGrid, target))
                continue;

            var live = _lock.CanLock(consoleUid, target);
            Vector2 tgtPos;
            Vector2 tgtVel;
            if (live)
            {
                tgtPos = _xform.GetWorldPosition(target);
                tgtVel = TryComp<PhysicsComponent>(target, out var tp) ? tp.LinearVelocity : Vector2.Zero;
            }
            else
            {
                tgtPos = console.CoastPos;
                tgtVel = console.CoastVel;
            }

            var mapId = Transform(ourGrid).MapID;
            var mapUid = Transform(ourGrid).MapUid;
            if (mapUid == null)
                continue;

            var missileQ = EntityQueryEnumerator<TargetGuidedComponent, ProjectileComponent, TransformComponent, PhysicsComponent>();
            while (missileQ.MoveNext(out var missile, out var guided, out var proj, out var mxform, out var mphys))
            {
                if (guided.ControllingConsole != consoleUid)
                    continue;
                if (!guided.AllowRadarTrack)
                    continue;

                if (!guided.UseRadarTrack)
                    _targetGuided.SetGuidanceMode(missile, true, guided);

                if (proj.Shooter is { } sh &&
                    Transform(sh).GridUid is { } sg &&
                    HasComp<FTLComponent>(sg))
                    continue;

                var speed = MathF.Max(guided.CurrentSpeed, guided.LaunchSpeed);
                if (speed < 1f)
                    speed = guided.MaxSpeed;
                var mVel = mphys.LinearVelocity;
                var mPos = _xform.GetWorldPosition(missile);
                var (lead, tti) = RadarLockSystem.ComputeMissileLeadPoint(mPos, mVel, tgtPos, tgtVel, speed);
                var range = (tgtPos - mPos).Length();
                if (range < 40f)
                {
                    var blend = Math.Clamp(range / 40f, 0f, 1f);
                    lead = Vector2.Lerp(tgtPos, lead, blend); // terminal phase
                }

                var mapCoords = new MapCoordinates(lead, mapId);
                var entityCoords = _xform.ToCoordinates(mapUid.Value, mapCoords);
                _targetGuided.SetRadarTrackTarget(missile, entityCoords, guided);
            }
        }

        var allGuided = EntityQueryEnumerator<TargetGuidedComponent>();
        while (allGuided.MoveNext(out var missile, out var guided))
        {
            if (!guided.UseRadarTrack)
                continue;
            if (guided.ControllingConsole is not { } c)
                continue;
            if (!TryComp<FireControlConsoleComponent>(c, out var console))
                continue;
            if (console.RadarTrackEnabled
                && _lock.IsLockValid(c, console)
                && console.LockQuality >= 0.2f
                && console.LockedTarget is { } t
                && Transform(c).GridUid is { } g
                && _lock.HasIllumination(g, t))
                continue;

            _targetGuided.SetGuidanceMode(missile, false, guided);
        }
    }
}
