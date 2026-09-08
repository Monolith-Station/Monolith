using System.Numerics;
using System.Linq;
using Content.Shared._CE.Planets.Shields;
using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Client._CE.Planets.Shields;

public abstract class CEShieldBeamOverlay : Overlay
{
    protected abstract Color EnergyColor { get; }
    protected abstract float CoreRadius { get; }
    protected abstract float FlowSpeed { get; }
    public float Phase;
    public readonly HashSet<EntityUid> SourceMaps = new();
    public readonly List<(int Depth, Vector2 Position, float Coverage)> Clouds = new();
    private readonly Vector2[] _vertices = new Vector2[4];
    private readonly ShaderInstance _shader = IoCManager.Resolve<IPrototypeManager>()
        .Index<ShaderPrototype>("CEShieldBeam").InstanceUnique();
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public readonly List<(int Depth, Vector2 Position)> Points = new();

    public void Render(DrawingHandleScreen handle, Vector2 size, float pixelsPerMeter, float time)
    {
        if (Points.Count == 0 || Up.LengthSquared() < 0.0001f)
            return;
        Points.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        var start = Points[0].Position;
        var end = start + Vector2.Normalize(Up) * (size.Length() + start.Length()) * 2;
        DrawColumn(handle, start, end, pixelsPerMeter, time, SourceMaps.Contains(Maps[Points[0].Depth]));
    }

    public readonly Dictionary<int, EntityUid> Maps = new();
    public Vector2 Up;

    private void DrawColumn(DrawingHandleScreen handle, Vector2 start, Vector2 end, float scale, float time, bool source)
    {
        var length = Vector2.Distance(start, end);
        if (length < 0.001f)
            return;
        var direction = (end - start) / length;
        var normal = new Vector2(-direction.Y, direction.X);
        var radius = CoreRadius * scale;
        _shader.SetParameter("BEAM_START", Points[0].Position);
        _shader.SetParameter("BEAM_CAP_START", start);
        _shader.SetParameter("BEAM_DIRECTION", direction);
        _shader.SetParameter("BEAM_RADIUS", MathF.Max(radius, 0.5f));
        _shader.SetParameter("BEAM_TIME", time * FlowSpeed);
        _shader.SetParameter("BEAM_PHASE", Phase);
        _shader.SetParameter("BEAM_SOURCE", source ? 1f : 0f);
        var deck = Vector3.Zero;
        var veil = Vector3.Zero;
        foreach (var cloud in Clouds.OrderBy(c => c.Depth))
        {
            var mask = new Vector3(cloud.Position, cloud.Coverage);
            if (cloud.Coverage >= 1f)
                deck = mask;
            else if (cloud.Coverage > 0f)
                veil = mask;
        }
        _shader.SetParameter("BEAM_CLOUD_DECK", deck);
        _shader.SetParameter("BEAM_CLOUD_VEIL", veil);
        _shader.SetParameter("BEAM_COLOR", new Vector3(EnergyColor.R, EnergyColor.G, EnergyColor.B));
        handle.UseShader(_shader);
        try
        {
            var extent = MathF.Max(radius, 0.5f) * 4f;
            Strip(handle, start - direction * (source ? extent : length), end, normal * extent, Color.White);
        }
        finally
        {
            handle.UseShader(null);
        }
    }

    private void Strip(DrawingHandleScreen handle, Vector2 start, Vector2 end, Vector2 halfWidth, Color color)
    {
        _vertices[0] = start - halfWidth;
        _vertices[1] = end - halfWidth;
        _vertices[2] = end + halfWidth;
        _vertices[3] = start + halfWidth;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _vertices, color);
    }

    protected override void DisposeBehavior()
    {
        _shader.Dispose();
        base.DisposeBehavior();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
    }
}

public sealed class CEMAWCShieldBeamOverlay : CEShieldBeamOverlay
{
    protected override Color EnergyColor => new(1f, 0.65f, 0.12f);
    protected override float CoreRadius => 0.22f;
    protected override float FlowSpeed => 12f;
}

public sealed class CEPlanetaryShieldBeamOverlay : CEShieldBeamOverlay
{
    protected override Color EnergyColor => new(0.35f, 0.7f, 1f);
    protected override float CoreRadius => 0.42f;
    protected override float FlowSpeed => 22f;
}

public sealed class CEShieldBeamOverlayDispatcher(IEntityManager entities) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly Dictionary<NetEntity, CEShieldBeamOverlay> _instances = new();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();

    public void RenderPass(IRenderHandle render, IClydeViewport viewport,
        Func<EntityUid, Vector2, (int Depth, Vector2 Position, Vector2 Up)?> project,
        Func<EntityUid, float> cloudCoverage, float pixelsPerMeter)
    {
        foreach (var instance in _instances.Values)
        {
            instance.Points.Clear();
            instance.Maps.Clear();
            instance.Clouds.Clear();
            instance.SourceMaps.Clear();
        }
        var transform = entities.System<SharedTransformSystem>();
        var query = entities.EntityQueryEnumerator<CEShieldBeamComponent, TransformComponent>();
        while (query.MoveNext(out _, out var beam, out var xform))
        {
            if (xform.MapUid is not { } pointMap || project(pointMap, transform.GetWorldPosition(xform)) is not { } point)
                continue;
            if (!_instances.TryGetValue(beam.Generator, out var instance))
            {
                instance = beam.Overlay switch
                {
                    "MAWC" => new CEMAWCShieldBeamOverlay(),
                    _ => new CEPlanetaryShieldBeamOverlay(),
                };
                instance.Phase = (beam.Generator.GetHashCode() & 1023) * 0.1f;
                _instances.Add(beam.Generator, instance);
            }
            instance.Points.Add((point.Depth, point.Position));
            if (beam.Source)
                instance.SourceMaps.Add(pointMap);
            instance.Maps[point.Depth] = pointMap;
            if (entities.HasComponent<CEZCloudLayerComponent>(pointMap))
                instance.Clouds.Add((point.Depth, point.Position, cloudCoverage(pointMap)));
            instance.Up = point.Up;
        }
        var handle = render.DrawingHandleScreen;
        handle.RenderInRenderTarget(viewport.RenderTarget, () =>
        {
            handle.UseShader(null);
            foreach (var instance in _instances.Values)
                instance.Render(handle, viewport.Size,
                    pixelsPerMeter,
                    (float) _timing.CurTime.TotalSeconds);
        }, null);
        foreach (var (id, instance) in _instances.ToArray())
        {
            if (instance.Points.Count != 0)
                continue;
            instance.Dispose();
            _instances.Remove(id);
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
    }

    protected override void DisposeBehavior()
    {
        foreach (var instance in _instances.Values)
            instance.Dispose();
        _instances.Clear();
        base.DisposeBehavior();
    }
}
