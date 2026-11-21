using System.Numerics;
using Content.Shared._Mono.Ships;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client._Mono.ShipShieldShell;

internal sealed partial class ShipShieldShellOverlay
{
    private const int WavesPerBatch = 8;
    private readonly List<Wave> _waves = new();
    private readonly List<ImpactBatch> _impactBatches = new();

    private void CollectWaves(EntityUid uid, MapId map, bool projected, TimeSpan spoolStartedAt)
    {
        _waves.Clear();
        foreach (var hit in feedback.ImpactBursts)
        {
            if (hit.Shield != uid || !projected && hit.Map != map || hit.Power <= 0f || hit.HitAt < spoolStartedAt)
                continue;

            var age = MathF.Max(0f, (float) (timing.FrameStartTime - hit.At).TotalSeconds) /
                (ShipShieldProjectorComponent.ImpactDuration * MathF.Sqrt(hit.Power));
            if (age < 1f)
                _waves.Add(new Wave(hit.Point, 0.95f * hit.Power * age, hit.Power * (1f - SmoothFade(0f, 1f, age))));
        }
    }

    private void DrawWaves(DrawingHandleBase handle, bool projected, int vertexCount, Vector2 radii, bool underside,
        float shatterAnimation, ref int shaderIndex)
    {
        if (projected && vertexCount == 0)
            return;

        var metric = radii / MathF.Max(radii.X, radii.Y);
        for (var offset = 0; offset < _waves.Count; offset += WavesPerBatch)
        {
            if (shaderIndex == _impactBatches.Count)
                _impactBatches.Add(new ImpactBatch(prototypes.Index(ImpactShader).InstanceUnique()));

            var batch = _impactBatches[shaderIndex++];
            var count = Math.Min(WavesPerBatch, _waves.Count - offset);
            for (var i = 0; i < count; i++)
            {
                var wave = _waves[offset + i];
                var center = WaveCenter(wave, underside ? -Vector3.UnitZ : Vector3.UnitZ);
                batch.Centers[i] = new Vector2(center.X, center.Y);
                batch.HeightsAndRadii[i] = new Vector2(center.Z, wave.Radius);
                batch.Strengths[i] = wave.Strength;
            }

            batch.Shader.SetParameter("WAVE_COUNT", (float) count);
            batch.Shader.SetParameter("WAVE_METRIC", metric);
            batch.Shader.SetParameter("WAVE_CENTERS", batch.Centers);
            batch.Shader.SetParameter("WAVE_HEIGHTS_RADII", batch.HeightsAndRadii);
            batch.Shader.SetParameter("WAVE_STRENGTHS", batch.Strengths);
            batch.Shader.SetParameter("SHATTER_ANIMATION", shatterAnimation);
            handle.UseShader(batch.Shader);
            if (projected)
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _surfaceTriangles.AsSpan(0, vertexCount));
            else
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, Texture.White, _vertices);
        }
    }

    private static Vector3 WaveCenter(Wave wave, Vector3 viewAxis)
    {
        var facing = Math.Clamp(Vector3.Dot(wave.Origin, viewAxis), -1f, 1f);
        var tilt = MathF.Min(wave.Radius * 0.9f, 0.8f) * (1f - SmoothFade(0.05f, 0.75f, MathF.Abs(facing)));
        if (tilt == 0f)
            return wave.Origin;

        // Open up rings at grazing angles without letting the front detach from the hit.
        var tangent = Vector3.Normalize(viewAxis - wave.Origin * facing);
        var (sin, cos) = MathF.SinCos(tilt);
        return wave.Origin * cos + tangent * sin;
    }

    private readonly record struct Wave(Vector3 Origin, float Radius, float Strength);

    private sealed class ImpactBatch(ShaderInstance shader)
    {
        public readonly ShaderInstance Shader = shader;
        public readonly Vector2[] Centers = new Vector2[WavesPerBatch];
        public readonly Vector2[] HeightsAndRadii = new Vector2[WavesPerBatch];
        public readonly float[] Strengths = new float[WavesPerBatch];
    }
}
