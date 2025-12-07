using System.Numerics;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.Physics.Controllers;
using Content.Server.Shuttles.Components;
using Content.Shared.CCVar;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.NPC.Events;
using Content.Shared.Physics;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
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

    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipSteererComponent, GetShuttleInputsEvent>(OnSteererGetInputs);

        Subs.CVar(_cfg, CCVars.NPCEnabled, enabled => _enabled = enabled, true);
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

        var shipUid = pilotXform.ParentUid;
        var shipXform = Transform(shipUid);
        if (!TryComp<ShuttleComponent>(shipUid, out var shuttle) || !TryComp<PhysicsComponent>(shipUid, out var shipBody))
        {
            ent.Comp.Status = ShipSteeringStatus.InRange;
            return;
        }

        args.GotInput = true;

        var target = ent.Comp.Coordinates;
        var mapTarget = _transform.ToMapCoordinates(target);

        var shipPos = _transform.GetMapCoordinates(shipXform);
        var shipNorthAngle = _transform.GetWorldRotation(shipUid);

        if (mapTarget.MapId != shipPos.MapId)
            return;

        var toTargetVec = mapTarget.Position - shipPos.Position;
        var toTargetAngle = toTargetVec.ToWorldAngle();
        var distance = toTargetVec.Length();
        // there's 500 different standards on how to count angles so needs the +PI
        var wishRotateBy = new Angle(ent.Comp.TargetRotation) + ShortestAngleDistance(shipNorthAngle + new Angle(Math.PI), toTargetAngle);

        var needBrake = ent.Comp.InRangeMaxSpeed != null;
        var maxArrivedVel = ent.Comp.InRangeMaxSpeed ?? 0.1f;
        var angVel = shipBody.AngularVelocity;

        var linVel = shipBody.LinearVelocity;

        if (distance <= ent.Comp.Range)
        {
            if (linVel.Length() <= maxArrivedVel && angVel < ent.Comp.MaxRotateRate && needBrake)
            {
                // all good, but keep braking
                ent.Comp.Status = ShipSteeringStatus.InRange;
                args.Input = new ShuttleInput(Vector2.Zero, 0f, 1f);
                return;
            }

            if (needBrake)
            {
                // close but moving, brake
                args.Input = new ShuttleInput(Vector2.Zero, 0f, 1f);
            }
            return;
        }

        ent.Comp.Status = ShipSteeringStatus.Moving;

        var angAccel = _mover.GetAngularAcceleration(shuttle, shipBody);
        var brakeAngleDelta = angAccel == 0f ? 0f : (angVel * angVel) / (2f * angAccel);
        brakeAngleDelta *= Math.Sign(angVel);
        var rotateDelta = ShortestAngleDistance(new Angle(brakeAngleDelta), wishRotateBy);
        var rotationInput = -(float)rotateDelta.Theta;
        rotationInput = MathF.Abs(rotationInput) < 0.01f ? 0f : MathF.Sign(rotationInput);

        var strafeInput = Vector2.Zero;

        // now calculate our braking path
        var brakeInput = 0f;
        var brakeThrust = _mover.GetDirectionThrust((-shipNorthAngle).RotateVec(-linVel), shuttle, shipBody) * ShuttleComponent.BrakeCoefficient;
        var brakeAccel = brakeThrust * shipBody.InvMass;
        var brakePath = linVel.Length() > 0 ? linVel.LengthSquared() / (2f * brakeAccel.Length()) : 0f;

        if (brakePath + ent.Comp.Range > distance && needBrake)
        {
            brakeInput = 1f;
        }
        else
        {
            var linVelDir = linVel.Length() == 0 ? Vector2.Zero : linVel.Normalized();
            var toTargetDir = toTargetVec.Normalized();
            // mirror linVelDir in relation to toTargetDir
            // for that we orthogonalize it then invert it to get the perpendicular-vector
            var adjustDir = -(linVelDir - toTargetDir * Vector2.Dot(linVelDir, toTargetDir));
            var globalStrafeInput = toTargetDir + adjustDir * 2;
            strafeInput = (-shipNorthAngle).RotateVec(globalStrafeInput);
        }

        args.Input = new ShuttleInput(strafeInput, rotationInput, brakeInput);
    }

    /// <summary>
    /// Adds the AI to the steering system to move towards a specific target
    /// </summary>
    public ShipSteererComponent Steer(EntityUid uid, EntityCoordinates coordinates, ShipSteererComponent? component = null)
    {
        var xform = Transform(uid);
        var shipUid = xform.ParentUid;
        if (TryComp<ShuttleComponent>(shipUid, out var shuttle))
            _mover.AddPilot(shipUid, uid);

        if (!Resolve(uid, ref component, false))
            component = AddComp<ShipSteererComponent>(uid);

        component.Coordinates = coordinates;

        return component;
    }

    /// <summary>
    /// Stops the steering behavior for the AI and cleans up.
    /// </summary>
    public void Stop(EntityUid uid, ShipSteererComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        RemComp<ShipSteererComponent>(uid);
    }
}
