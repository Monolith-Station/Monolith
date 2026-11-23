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
            if (!_lock.HasGuidanceCapableRadar(ourGrid))
                continue;
            if (console.LockedTarget is not { } target)
                continue;

            var tgtPos = _xform.GetWorldPosition(target);
            var tgtVel = TryComp<PhysicsComponent>(target, out var tp) ? tp.LinearVelocity : Vector2.Zero;
            var ourPos = _xform.GetWorldPosition(ourGrid);
            var ourVel = TryComp<PhysicsComponent>(ourGrid, out var op) ? op.LinearVelocity : Vector2.Zero;
            var mapId = Transform(ourGrid).MapID;
            var mapUid = Transform(ourGrid).MapUid;
            if (mapUid == null)
                continue;

            var missileQ = EntityQueryEnumerator<TargetGuidedComponent, ProjectileComponent, TransformComponent>();
            while (missileQ.MoveNext(out var missile, out var guided, out var proj, out _))
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

                var speed = guided.CurrentSpeed > 1f ? guided.CurrentSpeed : guided.MaxSpeed;
                var lead = RadarLockSystem.ComputeLeadPoint(ourPos, ourVel, tgtPos, tgtVel, speed);

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
            if (console.RadarTrackEnabled && _lock.IsLockValid(c, console))
                continue;

            _targetGuided.SetGuidanceMode(missile, false, guided);
        }
    }
}
