using System.Numerics;
using Content.Client._CE.Planets.Hellpods;
using Content.Shared._CE.Planets.Hellpods;
using Content.Shared._CE.Planets.Shields;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._CE.Planets.Shields;

public sealed partial class CEPlanetShieldVisualSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    private readonly record struct ShieldAnimation(bool Active, TimeSpan Transition, TimeSpan LastSeen, float Phase);
    private readonly Dictionary<EntityUid, ShieldAnimation> _animations = new();
    private readonly Dictionary<(EntityUid Planet, Vector2 Entry, TimeSpan Time), TimeSpan> _impactStarts = new();
    private readonly HashSet<(Vector2 Entry, TimeSpan Time)> _drawnImpacts = new();
    private readonly Vector2[] _impactCenters = new Vector2[CEPlanetShieldComponent.MaxImpacts];
    private readonly float[] _impactAges = new float[CEPlanetShieldComponent.MaxImpacts];
    private readonly float[] _impactStrengths = new float[CEPlanetShieldComponent.MaxImpacts];

    public override void FrameUpdate(float frameTime)
    {
        var staleBefore = _timing.CurTime - TimeSpan.FromSeconds(2f * CEPlanetShieldComponent.ImpactLifetime);
        foreach (var key in _impactStarts.Keys)
        {
            if (key.Time <= staleBefore)
                _impactStarts.Remove(key);
        }

        var now = _timing.FrameStartTime;
        foreach (var (uid, animation) in _animations)
        {
            if (now - animation.LastSeen > TimeSpan.FromSeconds(0.25))
                _animations.Remove(uid);
        }
    }

    internal static bool SupportsShader(string shader) =>
        shader is "RockyPlanet" or "IceMoon" or "GasGiant" or "IceGiant";

    internal static void InitializeShader(ShaderInstance shader)
    {
        shader.SetParameter("skin_color", Color.FromHex("#2e8fb8").WithAlpha(0.7f));
        shader.SetParameter("brightness", 1f);
        shader.SetParameter("pixel_grid", 0f);
        shader.SetParameter("hex_density", 9f);
        shader.SetParameter("form_origin", new Vector2(0f, -0.85f));
        shader.SetParameter("fill_level", 0.08f);
        shader.SetParameter("line_level", 0.5f);
        shader.SetParameter("rim_level", 0.75f);
        shader.SetParameter("core_fade", 0f);
        shader.SetParameter("shard_scale", 4f);
        shader.SetParameter("alpha_bands", 6f);
        shader.SetParameter("breath_depth", 0.08f);
    }

    internal void Apply(ShaderInstance shader, EntityUid? planet, EntityUid viewMap,
        CEHellpodApproachComponent? approach, CEHellpodComponent? riderPod)
    {
        var shield = CompOrNull<CEPlanetShieldComponent>(planet);
        shader.SetParameter("progress", planet is { } uid ? GetPhase(uid, shield) : 0f);
        shader.SetParameter("planetImpactCount", 0f);
        if (planet is not { } body)
            return;

        var count = 0;
        var now = _timing.FrameStartTime;
        var curTime = _timing.CurTime;
        _drawnImpacts.Clear();

        void AddImpact(CEPlanetShieldImpact impact)
        {
            if (count >= _impactCenters.Length || impact.Time > curTime || !_drawnImpacts.Add((impact.Entry, impact.Time)))
                return;

            var key = (body, impact.Entry, impact.Time);
            if (!_impactStarts.TryGetValue(key, out var start))
            {
                if ((curTime - impact.Time).TotalSeconds >= CEPlanetShieldComponent.ImpactLifetime)
                    return;

                _impactStarts[key] = start = now;
            }

            var age = (float) (now - start).TotalSeconds;
            if (age >= CEPlanetShieldComponent.ImpactLifetime)
                return;

            _impactCenters[count] = new Vector2(impact.Entry.X, -impact.Entry.Y);
            _impactStrengths[count] = impact.Strength;
            _impactAges[count++] = MathF.Max(age, 0f) / CEPlanetShieldComponent.ImpactLifetime;
        }

        if (approach != null)
        {
            if (riderPod != null && CEHellpodDepartureSystem.ShouldRicochet(EntityManager, body, riderPod.Ricochet))
                AddImpact(new(riderPod.OrbitalEntry, approach.End, CEHellpodComponent.RicochetRippleStrength));
        }
        else if (shield is { Active: true })
        {
            foreach (var impact in shield.Impacts)
                AddImpact(impact);

            var departures = AllEntityQuery<CEHellpodDepartureComponent, TransformComponent>();
            while (departures.MoveNext(out var departure, out var xform))
            {
                if (xform.MapUid == viewMap && departure.Planet == body)
                    AddImpact(new(departure.Entry, departure.End,
                        departure.Ricochet != null ? CEHellpodComponent.RicochetRippleStrength : 1f));
            }
        }

        if (count == 0)
            return;

        shader.SetParameter("planetImpactCount", (float) count);
        shader.SetParameter("planetImpactCenters", _impactCenters);
        shader.SetParameter("planetImpactAges", _impactAges);
        shader.SetParameter("planetImpactStrengths", _impactStrengths);
    }

    private float GetPhase(EntityUid uid, CEPlanetShieldComponent? shield)
    {
        var active = shield?.Active ?? false;
        var transition = shield?.TransitionAt ?? TimeSpan.Zero;
        var now = _timing.FrameStartTime;
        var phase = shield?.PhaseAt(_timing.CurTime) ?? 0f;
        if (_animations.TryGetValue(uid, out var previous) &&
            now - previous.LastSeen <= TimeSpan.FromSeconds(0.25))
        {
            if (previous.Active == active && previous.Transition == transition)
            {
                var step = (float) (now - previous.LastSeen).TotalSeconds / CEPlanetShieldComponent.TransitionSeconds;
                phase = Math.Clamp(previous.Phase + (active ? step : -step), 0f, 1f);
            }
            else if (previous.Active != active &&
                     _timing.CurTime - transition < TimeSpan.FromSeconds(CEPlanetShieldComponent.TransitionSeconds))
            {
                phase = previous.Phase;
            }
        }

        _animations[uid] = new ShieldAnimation(active, transition, now, phase);
        return phase;
    }
}
