using Content.Shared._Mono.Sensors;

namespace Content.Server._Mono.Sensors;

public sealed class RadarCountermeasureSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public float GetActiveRadarMultiplier(EntityUid radarUid, Angle radarWorldBearing, float chaffResistance)
    {
        if (!TryComp(radarUid, out TransformComponent? radarXform))
            return 1f;

        var radarPos = _xform.GetWorldPosition(radarUid);
        var worstAttenuation = 0f;

        var q = EntityQueryEnumerator<RadarCountermeasureComponent, TransformComponent>();
        while (q.MoveNext(out var uid, out var cm, out var xform))
        {
            if (!cm.Active)
                continue;

            var cmPos = _xform.GetWorldPosition(uid);
            var toCm = cmPos - radarPos;
            var dist = toCm.Length();
            if (dist < 0.01f || dist > cm.MaxEffectRange)
                continue;

            var bearingToCm = toCm.ToWorldAngle();
            var halfAngle = MathHelper.DegreesToRadians(
                MathF.Max(cm.MinHalfAngleDegrees, MathHelper.RadiansToDegrees(MathF.Atan2(cm.Radius, dist))));

            var delta = Angle.ShortestDistance(radarWorldBearing, bearingToCm);
            if (Math.Abs(delta.Theta) > halfAngle)
                continue;

            var edge = 1f - (float)(Math.Abs(delta.Theta) / halfAngle);
            var rangeFalloff = 1f - Math.Clamp(dist / cm.MaxEffectRange, 0f, 1f);
            var att = cm.Strength * edge * rangeFalloff;
            if (att > worstAttenuation)
                worstAttenuation = att;
        }

        var resisted = worstAttenuation * (1f - Math.Clamp(chaffResistance, 0f, 1f));
        return Math.Clamp(1f - resisted, 0f, 1f);
    }
}
