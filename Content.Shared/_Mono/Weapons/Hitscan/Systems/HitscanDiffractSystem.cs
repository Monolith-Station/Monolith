using System.Numerics;
using Content.Shared.Physics;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed class HitscanDiffractSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HitscanDiffractComponent, HitscanTraceEvent>(OnHitscanTrace, before: [typeof(HitscanBasicRaycastSystem)]);
    }

    private void OnHitscanTrace(Entity<HitscanDiffractComponent> hitscan, ref HitscanTraceEvent args)
    {
        var shooter = args.Shooter ?? args.Gun;
        var mapCoords = _transform.ToMapCoordinates(args.FromCoordinates);

        // Use BulletImpassable to narrow down list of entities intersected (maybe make this a component in the future?)
        var diffractCheckRay = new CollisionRay(mapCoords.Position, args.ShotDirection, (int)CollisionGroup.BulletImpassable);
        var diffractCheckResults = _physics.IntersectRay(mapCoords.MapId, diffractCheckRay, 20.0f, shooter, false);

        // Look for the first entity with diffraction target component
        EntityUid? diffractorEntity = null;
        Vector2 hitPoint = default;

        foreach (var result in diffractCheckResults)
        {
            if (!TryComp<HitscanDiffractTargetComponent>(result.HitEntity, out var target))
                continue;

            if (!target.Active)
                continue;

            diffractorEntity = result.HitEntity;
            hitPoint = result.HitPos;
            break;
        }

        if (diffractorEntity == null)
            return;  // Let the original hitscan continue through glass

        // Delete original hitspan if hitscanDiffractTarget entity is found
        QueueDel(hitscan);

        var hitMapCoordinates = new MapCoordinates(hitPoint, mapCoords.MapId);
        var hitCoordinates = _transform.ToCoordinates(args.FromCoordinates.EntityId, hitMapCoordinates);

        var beamCount = hitscan.Comp.BeamCount;
        var spreadAngle = hitscan.Comp.SpreadAngle;

        // Replace original hitscan with diffracted beams
        if (beamCount == 1)
        {
            SpawnDiffractedBeam(hitscan, args, hitCoordinates, args.ShotDirection);
            return;
        }

        // Apply spread if beamCount is more than 1
        var startAngle = -spreadAngle / 2;
        var angleIncrement = beamCount > 1 ? spreadAngle / (beamCount - 1) : 0;

        for (int i = 0; i < beamCount; i++)
        {
            var angle = startAngle + (angleIncrement * i);
            var newDirection = RotateVector(args.ShotDirection, angle);
            SpawnDiffractedBeam(hitscan, args, hitCoordinates, newDirection);
        }
    }

    private void SpawnDiffractedBeam(Entity<HitscanDiffractComponent> originalHitscan,
        HitscanTraceEvent originalArgs, EntityCoordinates fromCoordinates, Vector2 direction)
    {
        // Which hitscan prototype to spawn?
        EntProtoId? prototypeToSpawn = originalHitscan.Comp.DiffractedBeamPrototype;

        // Safeguard against diffract beam hell (stack overflow)
        if (prototypeToSpawn != null)
        {
            var prototype = _prototypeManager.Index<EntityPrototype>(prototypeToSpawn.Value);
            if (prototype.Components.ContainsKey("HitscanDiffract"))
            {
                prototypeToSpawn = new EntProtoId("RedLaser");
            }
        }
        else
        {
            prototypeToSpawn = new EntProtoId("RedLaser");
        }

        // Spawn entity
        var newHitscan = Spawn(prototypeToSpawn.Value, fromCoordinates);

        // Fire
        var diffractedTraceEvent = new HitscanTraceEvent
        {
            FromCoordinates = fromCoordinates,
            ShotDirection = direction,
            Gun = originalArgs.Gun,
            Shooter = originalArgs.Shooter,
        };

        RaiseLocalEvent(newHitscan, ref diffractedTraceEvent);
    }

    private static Vector2 RotateVector(Vector2 vector, float angleRadians)
    {
        var cos = MathF.Cos(angleRadians);
        var sin = MathF.Sin(angleRadians);
        return new Vector2(
            vector.X * cos - vector.Y * sin,
            vector.X * sin + vector.Y * cos
        );
    }
}
