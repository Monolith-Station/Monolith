using System.Numerics;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Map;
using Robust.Shared.Spawners;

namespace Content.Server._Mono.Radar;

public sealed partial class HitscanRadarSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Listen for fire events on anything that has a HitscanRadarSignatureComponent
        SubscribeLocalEvent<HitscanRadarSignatureComponent, HitscanRaycastFiredEvent>(OnHitscanRaycastFired);
    }

    private void OnHitscanRaycastFired(Entity<HitscanRadarSignatureComponent> ent, ref HitscanRaycastFiredEvent ev)
    {
        var shooter = ev.Shooter ?? ev.Gun; // If "there is no shooter" then the shooter is a gun
        var shooterCoords = new EntityCoordinates(shooter, Vector2.Zero);
        var radarEntity = Spawn(null, shooterCoords);
        var radarComponent = EnsureComp<HitscanRadarComponent>(radarEntity);
        var startPos = _transform.ToMapCoordinates(ev.FromCoordinates).Position;
        var endPos = startPos + ev.ShotDirection.Normalized() * ev.DistanceTried;
        // Copy visuals from the hitscan signature on the laser entity, not the shooter.
        ApplySignatureSettings(ent.Comp, radarComponent);

        radarComponent.StartPosition = startPos;
        radarComponent.EndPosition = endPos;
        radarComponent.OriginGrid = Transform(shooter).GridUid;

        ScheduleEntityDespawn(radarEntity, radarComponent.LifeTime); // Make sure the radar entity gets cleaned up
    }

    private static void ApplySignatureSettings(HitscanRadarSignatureComponent signature, HitscanRadarComponent radarComponent)
    {
        radarComponent.RadarColor = signature.RadarColor;
        radarComponent.LineThickness = signature.LineThickness;
        radarComponent.Enabled = signature.Enabled;
        radarComponent.LifeTime = signature.LifeTime;
    }
    private void ScheduleEntityDespawn(EntityUid entity, float lifetime)
    {
        var despawnComponent = EnsureComp<TimedDespawnComponent>(entity);
        despawnComponent.Lifetime = lifetime;
    }
}
