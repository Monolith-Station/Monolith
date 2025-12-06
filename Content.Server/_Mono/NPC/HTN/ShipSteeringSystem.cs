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

    private void OnSteererGetInputs(Entity<ShipSteererComponent> ent, ref GetShuttleInputsEvent args)
    {
        args.GotInput = true;
        var pilotXform = Transform(ent);

        var shipUid = pilotXform.ParentUid;
        var shipXform = Transform(shipUid);
        var shipBody = Comp<PhysicsComponent>(shipUid);
        if (!TryComp<ShuttleComponent>(shipUid, out var shuttle))
            return;

        var target = ent.Comp.Coordinates;
        var mapTarget = _transform.ToMapCoordinates(target);

        var shipPos = _transform.GetMapCoordinates(shipXform);
        var shipNorthAngle = _transform.GetWorldRotation(shipUid);

        if (mapTarget.MapId != shipPos.MapId)
            return;

        var toTargetVec = shipPos.Position - mapTarget.Position;
        var toTargetAngle = toTargetVec.ToWorldAngle();
        var distance = toTargetVec.Length();
        var angleDelta = toTargetAngle - shipNorthAngle;
        var wishRotateBy = angleDelta + Angle.ShortestDistance(shipNorthAngle, new Angle(toTargetAngle));

        var maxArrivedVel = ent.Comp.InRangeMaxSpeed ?? 0.1f;
        var angVel = shipBody.AngularVelocity;

        var linVel = shipBody.LinearVelocity;

        if (distance <= ent.Comp.Range)
        {
            if (linVel.Length() <= maxArrivedVel && angVel < ent.Comp.MaxRotateRate)
            {
                // all good, but keep braking
                ent.Comp.Status = ShipSteeringStatus.InRange;
                args.Input = new ShuttleInput(Vector2.Zero, 0f, 1f);
                return;
            }

            // close but moving, brake
            args.Input = new ShuttleInput(Vector2.Zero, 0f, 1f);
            return;
        }

        ent.Comp.Status = ShipSteeringStatus.Moving;

        var angAccel = _mover.GetAngularAcceleration(shuttle, shipBody);
        var brakeAngleDelta = angAccel == 0f ? 0f : (angVel * angVel) / (2f * angAccel);
        var rotateDelta = Angle.ShortestDistance(new Angle(brakeAngleDelta), wishRotateBy);
        var rotationInput = (float)rotateDelta.Theta;
        rotationInput = MathF.Abs(rotationInput) < 0.01f ? 0f : rotationInput;

        var strafeInput = Vector2.Zero;

        // now calculate our braking path
        var brakeInput = 0f;
        var brakeThrust = _mover.GetDirectionThrust(-linVel, shuttle, shipBody) * ShuttleComponent.BrakeCoefficient;
        var brakePath = linVel.LengthSquared() / (2f * brakeThrust.Length());

        if (brakePath > distance)
        {
            brakeInput = 1f;
        }
        else
        {
            var linVelDir = shipBody.LinearVelocity.Normalized();
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
