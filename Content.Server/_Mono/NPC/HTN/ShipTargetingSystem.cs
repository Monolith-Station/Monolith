using Content.Server._Mono.FireControl;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Spawners;
using System.Numerics;

namespace Content.Server._Mono.NPC.HTN;

public sealed partial class ShipTargetingSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly FireControlSystem _cannon = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<GunComponent> _gunQuery;
    private EntityQuery<PhysicsComponent> _physQuery;

    public override void Initialize()
    {
        base.Initialize();

        _gunQuery = GetEntityQuery<GunComponent>();
        _physQuery = GetEntityQuery<PhysicsComponent>();
    }

    // have to use this because RT's is broken and unusable for navigation
    // another algorithm stolen from myself from orbitfight
    public Angle ShortestAngleDistance(Angle from, Angle to)
    {
        var diff = (to - from) % Math.Tau;
        return diff + Math.Tau * (diff < -Math.PI ? 1 : diff > Math.PI ? -1 : 0);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ShipTargetingComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            var pilotXform = Transform(uid);

            var shipUid = pilotXform.GridUid;

            var target = comp.Target;
            var targetUid = target.EntityId; // if we have a target try to lead it

            if (shipUid == null
                || TerminatingOrDeleted(targetUid)
                || !_physQuery.TryComp(shipUid, out var shipBody)
                || !TryComp<MapGridComponent>(shipUid, out var shipGrid)
            )
                continue;

            var targetGrid = Transform(targetUid).GridUid;

            var shipXform = Transform(shipUid.Value);

            var mapTarget = _transform.ToMapCoordinates(target);
            var shipPos = _transform.GetMapCoordinates(shipXform);

            // we or target might just be in FTL so don't count us as finished
            if (mapTarget.MapId != shipPos.MapId)
                continue;

            var linVel = shipBody.LinearVelocity;
            var targetVel = targetGrid == null ? Vector2.Zero : _physics.GetMapLinearVelocity(targetGrid.Value);
            var leadBy = 1f - MathF.Pow(1f - comp.LeadingAccuracy, frameTime);
            comp.CurrentLeadingVelocity = Vector2.Lerp(comp.CurrentLeadingVelocity, targetVel, leadBy);

            comp.WeaponCheckAccum -= frameTime;
            if (comp.WeaponCheckAccum < 0f)
            {
                comp.Cannons.Clear();
                var cannons = new HashSet<Entity<FireControllableComponent>>();
                _lookup.GetLocalEntitiesIntersecting(shipUid.Value, shipGrid.LocalAABB, cannons);
                foreach (var cannon in cannons)
                {
                    comp.Cannons.Add(cannon);
                }
                comp.WeaponCheckAccum += comp.WeaponCheckSpacing;
            }

            FireWeapons(shipUid.Value, comp.Cannons, mapTarget, linVel, comp.CurrentLeadingVelocity);
        }
    }

    private void FireWeapons(EntityUid shipUid, List<EntityUid> cannons, MapCoordinates destMapPos, Vector2 ourVel, Vector2 otherVel)
    {
        var shipXform = Transform(shipUid);
        if (!_physQuery.TryComp(shipUid, out var shipBody))
            return;

        if (!_cannon.CanFireWeapons(shipUid))
            return;

        var shipAngVel = shipBody.AngularVelocity;
        var shipCenter = shipBody.LocalCenter;

        foreach (var uid in cannons)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            var gXform = Transform(uid);

            if (!gXform.Anchored || !_gunQuery.TryComp(uid, out var gun))
                continue;

            var hitTime = 0f;
            var leadBy = Vector2.Zero;
            if (_gun.TryNextShootPrototype((uid, gun), out var proto))
            {
                var gunToDestVec = destMapPos.Position - _transform.GetWorldPosition(gXform);

                if (proto.TryGetComponent<HitscanAmmoComponent>(out var hitscan, Factory))
                {
                    // check if too far
                    if (proto.TryGetComponent<HitscanBasicRaycastComponent>(out var raycast, Factory)
                        && raycast.MaxDistance < gunToDestVec.Length()
                    )
                        continue;
                }
                else
                {
                    var centerToGunVec = gXform.LocalPosition - shipBody.LocalCenter;
                    // rotate 90deg left
                    var gunAngVel = new Vector2(-centerToGunVec.Y, centerToGunVec.X) * shipAngVel;
                    gunAngVel = shipXform.LocalRotation.RotateVec(gunAngVel);
                    leadBy = otherVel - ourVel - gunAngVel;

                    var gunToDestDir = NormalizedOrZero(gunToDestVec);

                    var projVel = gun.ProjectileSpeedModified;
                    var normVel = gunToDestDir * Vector2.Dot(leadBy, gunToDestDir);
                    var tgVel = leadBy - normVel;
                    // going too fast to the side, we can't possibly hit it
                    if (tgVel.Length() > projVel)
                        continue;

                    var normTarget = gunToDestDir * MathF.Sqrt(projVel * projVel - tgVel.LengthSquared());
                    // going too fast away, we can't hit it
                    if (Vector2.Dot(normTarget, normVel) > 0f && normVel.Length() > normTarget.Length())
                        continue;

                    var approachVel = (normTarget - normVel).Length();
                    hitTime = gunToDestVec.Length() / approachVel;

                    // might take too long to hit
                    var bulletProto = _gun.GetBulletPrototype(proto);
                    if (bulletProto.TryGetComponent<TimedDespawnComponent>(out var despawn, Factory) && hitTime > despawn.Lifetime)
                        continue;
                }
            }

            var targetMapPos = destMapPos.Offset(leadBy * hitTime);

            _cannon.AttemptFire(uid, uid, _transform.ToCoordinates(targetMapPos), noServer: true);
        }
    }

    public Vector2 NormalizedOrZero(Vector2 vec)
    {
        return vec.LengthSquared() == 0 ? Vector2.Zero : vec.Normalized();
    }

    /// <summary>
    /// Adds the AI to the steering system to move towards a specific target.
    /// Returns null on failure.
    /// </summary>
    public ShipTargetingComponent? Target(Entity<ShipTargetingComponent?> ent, EntityCoordinates coordinates)
    {
        var xform = Transform(ent);
        var shipUid = xform.GridUid;

        if (!Resolve(ent, ref ent.Comp, false))
            ent.Comp = AddComp<ShipTargetingComponent>(ent);

        ent.Comp.Target = coordinates;

        return ent.Comp;
    }

    /// <summary>
    /// Stops the steering behavior for the AI and cleans up.
    /// </summary>
    public void Stop(Entity<ShipTargetingComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        RemComp<ShipTargetingComponent>(ent);
    }
}
