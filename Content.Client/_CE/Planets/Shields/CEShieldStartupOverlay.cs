using System.Numerics;
using Content.Client.Viewport;
using Content.Shared._CE.Planets.Shields;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._CE.Planets.Shields;

internal sealed class CEShieldStartupOverlay(
    IEntityManager entities,
    IGameTiming timing,
    IPrototypeManager prototypes) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private readonly List<ShaderInstance> _shaders = new();

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye is ScalingViewport.ZEye { PlanetTransitBackdrop: true })
            return;

        var index = 0;
        var now = timing.CurTime;
        var transform = entities.System<SharedTransformSystem>();
        var generators = entities.EntityQueryEnumerator<CEShieldGeneratorComponent, TransformComponent>();
        while (generators.MoveNext(out var uid, out var generator, out var xform))
        {
            if (xform.MapID != args.MapId || generator.Stage != CEShieldGeneratorStage.SpinningUp ||
                generator.SpinupTime <= TimeSpan.Zero)
                continue;

            var remaining = generator.SpinupEndsAt - now;
            if (generator.SpinupEndsAt == TimeSpan.Zero || remaining > generator.SpinupTime)
                continue;

            var spec = generator.BeamOverlay == "MAWC"
                ? CEMAWCShieldBeamOverlay.Startup
                : CEPlanetaryShieldBeamOverlay.Startup;
            var timeline = spec.GetTimeline();
            var progress = 1.0 - Math.Max(0.0, remaining.TotalSeconds) / generator.SpinupTime.TotalSeconds;
            var time = (float) progress * Math.Max(80f, timeline.DrainEnd);
            var center = Vector2.Transform(generator.BeamOffset, transform.GetWorldMatrix(xform));
            DrawEffect(args, spec, timeline, center, time, uid.GetHashCode(), ref index);
        }

        if (index != 0)
            args.WorldHandle.UseShader(null);
    }

    private void DrawEffect(in OverlayDrawArgs args, CEShieldStartupVisuals spec, CEShieldStartupTimeline timeline,
        Vector2 center, float time, int seed, ref int index)
    {
        var growth = Math.Clamp((time - timeline.Burst) / (timeline.End - timeline.Burst), 0f, 1f);
        var shockwave = Math.Clamp((time - timeline.Burst) / Math.Max(0.001f, spec.ShockwaveSeconds), 0f, 1f);
        var radius = time < timeline.Burst
            ? spec.BurstRadius * (time / timeline.Burst)
            : spec.BurstRadius + (spec.EndRadius - spec.BurstRadius) * growth;
        var moteRange = Math.Max(0.01f, spec.MoteRange);
        var motesVisible = timeline.DrainEnd > timeline.End && time >= timeline.Burst && time < timeline.DrainEnd;
        var extent = motesVisible ? radius + moteRange + 0.12f : radius + 0.1f;
        if (shockwave > 0f && shockwave < 1f)
            extent = Math.Max(extent, spec.ShockwaveRadius + 0.15f);

        var bounds = Box2.CenteredAround(center, new Vector2(extent * 2f));
        if (!args.WorldAABB.Intersects(bounds))
            return;

        if (index == _shaders.Count)
            _shaders.Add(prototypes.Index<ShaderPrototype>("CEShieldStartup").InstanceUnique());

        var shader = _shaders[index++];
        shader.SetParameter("CENTER", center);
        shader.SetParameter("RADIUS", radius);
        shader.SetParameter("SEED", (seed & 65535) / 997f);
        shader.SetParameter("FLOW", time * spec.FlowSpeed);
        shader.SetParameter("ENERGY_COLOR", new Vector3(spec.EnergyColor.R, spec.EnergyColor.G, spec.EnergyColor.B));
        shader.SetParameter("WAVE_PHASE", shockwave);
        shader.SetParameter("WAVE_RADIUS", spec.ShockwaveRadius);
        shader.SetParameter("MOTE_TIME", time);
        shader.SetParameter("MOTE_SPEEDS", new Vector2(
            CEShieldStartupTimeline.ClampMoteSpeed(spec.MoteStartSpeed),
            CEShieldStartupTimeline.ClampMoteSpeed(spec.MoteEndSpeed)));
        shader.SetParameter("MOTE_SPEED_CURVE", (float) spec.MoteSpeedCurve);
        shader.SetParameter("MOTE_SPEED_VARIANCE", Math.Max(0f, spec.MoteSpeedVariance));
        shader.SetParameter("MOTE_COUNTS", motesVisible
            ? new Vector2(Math.Clamp(spec.MoteStartCount, 0, 256), Math.Clamp(spec.MoteEndCount, 0, 256))
            : Vector2.Zero);
        shader.SetParameter("MOTE_COUNT_CURVE", (float) spec.MoteCountCurve);
        shader.SetParameter("MOTE_COUNT_ROUGHNESS", Math.Clamp(spec.MoteCountRoughness, 0f, 1f));
        shader.SetParameter("MOTE_START", timeline.Burst);
        shader.SetParameter("MOTE_END", timeline.End);
        shader.SetParameter("MOTE_RANGE", moteRange);
        args.WorldHandle.UseShader(shader);
        args.WorldHandle.DrawRect(bounds, Color.White);
    }

    protected override void DisposeBehavior()
    {
        foreach (var shader in _shaders)
            shader.Dispose();

        base.DisposeBehavior();
    }
}
