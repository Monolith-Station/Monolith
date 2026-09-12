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

public abstract class CEShieldBeamOverlay : IDisposable
{
    protected abstract Color EnergyColor { get; }
    protected abstract float CoreRadius { get; }
    protected abstract float FlowSpeed { get; }
    private const float TipLength = 4f;
    private const float TipFadeLength = 1.25f;
    public float Phase;
    public readonly List<(int Depth, Vector2 Position, float Coverage)> Clouds = new();
    private readonly Vector2[] _vertices = new Vector2[4];
    private readonly ShaderInstance _shader = IoCManager.Resolve<IPrototypeManager>()
        .Index<ShaderPrototype>("CEShieldBeam").InstanceUnique();
    public readonly List<(int Depth, Vector2 Position, Vector2 Up)> Points = new();

    public void Render(DrawingHandleBase handle, float pixelsPerMeter, float time,
        int? fromDepth = null, int? toDepth = null, bool tip = true)
    {
        if (Points.Count == 0)
            return;

        Points.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        var sourceDepth = Points[0].Depth;
        var topDepth = Points[^1].Depth;
        var beamStart = PositionAt(sourceDepth);
        var top = PositionAt(topDepth);
        var topUp = PositionAt(topDepth + 1) - top;
        if (topUp.LengthSquared() < 0.0001f)
            return;
        var beamEnd = top + Vector2.Normalize(topUp) * TipLength * pixelsPerMeter;
        var from = Math.Max(fromDepth ?? sourceDepth, sourceDepth);
        var to = Math.Min(toDepth ?? topDepth, topDepth);

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

        var start = PositionAt(from);
        var startDepth = from;
        var flowOffset = PathLength(sourceDepth, from);
        foreach (var point in Points)
        {
            if (point.Depth <= from || point.Depth >= to)
                continue;
            DrawColumn(handle, start, point.Position, beamStart, beamEnd,
                JoinAt(startDepth), JoinAt(point.Depth), deck, veil, pixelsPerMeter, time, flowOffset, false);
            flowOffset += Vector2.Distance(start, point.Position);
            start = point.Position;
            startDepth = point.Depth;
        }

        if (to > from)
        {
            var end = PositionAt(to);
            DrawColumn(handle, start, end, beamStart, beamEnd,
                JoinAt(startDepth), JoinAt(to), deck, veil, pixelsPerMeter, time, flowOffset, false);
            flowOffset += Vector2.Distance(start, end);
            start = end;
        }
        if (tip)
            DrawColumn(handle, start, beamEnd, beamStart, beamEnd,
                JoinAt(to), Vector2.Zero, deck, veil, pixelsPerMeter, time, flowOffset, true);
    }

    private Vector2 PositionAt(float depth)
    {
        var first = Points[0];
        if (depth <= first.Depth)
            return first.Position + first.Up * (depth - first.Depth);

        for (var i = 1; i < Points.Count; i++)
        {
            var next = Points[i];
            var previous = Points[i - 1];
            if (depth > next.Depth || next.Depth == previous.Depth)
                continue;
            var t = (depth - previous.Depth) / (next.Depth - previous.Depth);
            return Vector2.Lerp(previous.Position, next.Position, t);
        }

        var last = Points[^1];
        return last.Position + last.Up * (depth - last.Depth);
    }

    private Vector2 JoinAt(float depth)
    {
        const float sample = 0.01f;
        var before = Vector2.Normalize(PositionAt(depth) - PositionAt(depth - sample));
        var after = Vector2.Normalize(PositionAt(depth + sample) - PositionAt(depth));
        var firstNormal = new Vector2(-before.Y, before.X);
        var secondNormal = new Vector2(-after.Y, after.X);
        var miter = firstNormal + secondNormal;
        if (miter.LengthSquared() < 0.0001f)
            return secondNormal;

        miter = Vector2.Normalize(miter);
        var denominator = Vector2.Dot(miter, secondNormal);
        return MathF.Abs(denominator) < 0.25f ? secondNormal : miter / denominator;
    }

    private float PathLength(float from, float to)
    {
        var start = PositionAt(from);
        var length = 0f;
        foreach (var point in Points)
        {
            if (point.Depth <= from || point.Depth >= to)
                continue;
            length += Vector2.Distance(start, point.Position);
            start = point.Position;
        }
        return length + Vector2.Distance(start, PositionAt(to));
    }

    private void DrawColumn(DrawingHandleBase handle, Vector2 start, Vector2 end, Vector2 beamStart,
        Vector2 beamEnd, Vector2 startJoin, Vector2 endJoin, Vector3 deck, Vector3 veil,
        float scale, float time, float flowOffset, bool tip)
    {
        var length = Vector2.Distance(start, end);
        if (length < 0.001f)
            return;
        var direction = (end - start) / length;
        var normal = new Vector2(-direction.Y, direction.X);
        var radius = CoreRadius * scale;
        var tipFade = TipFadeLength * scale;
        _shader.SetParameter("BEAM_START", start);
        _shader.SetParameter("BEAM_CAP_START", beamStart);
        _shader.SetParameter("BEAM_CAP_END", beamEnd);
        _shader.SetParameter("BEAM_DIRECTION", direction);
        _shader.SetParameter("BEAM_RADIUS", MathF.Max(radius, 0.5f));
        _shader.SetParameter("BEAM_TIP_FADE", MathF.Max(tipFade, 0.5f));
        _shader.SetParameter("BEAM_TIME", time * FlowSpeed);
        _shader.SetParameter("BEAM_PHASE", Phase);
        _shader.SetParameter("BEAM_FLOW_OFFSET", flowOffset / MathF.Max(radius, 0.5f));
        _shader.SetParameter("BEAM_CLOUD_DECK", deck);
        _shader.SetParameter("BEAM_CLOUD_VEIL", veil);
        _shader.SetParameter("BEAM_COLOR", new Vector3(EnergyColor.R, EnergyColor.G, EnergyColor.B));
        handle.UseShader(_shader);
        try
        {
            var extent = MathF.Max(radius, 0.5f) * 4f;
            var source = Vector2.DistanceSquared(start, beamStart) < 0.01f;
            var startWidth = startJoin.LengthSquared() > 0f ? startJoin * extent : normal * extent;
            var endWidth = endJoin.LengthSquared() > 0f ? endJoin * extent : normal * extent;
            Strip(handle, start - direction * (source ? extent : 0f),
                end + direction * (tip ? tipFade * 0.25f : 0f), startWidth, endWidth, Color.White);
        }
        finally
        {
            handle.UseShader(null);
        }
    }

    private void Strip(DrawingHandleBase handle, Vector2 start, Vector2 end, Vector2 startWidth,
        Vector2 endWidth, Color color)
    {
        _vertices[0] = start - startWidth;
        _vertices[1] = end - endWidth;
        _vertices[2] = end + endWidth;
        _vertices[3] = start + startWidth;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _vertices, color);
    }

    public void Dispose()
    {
        _shader.Dispose();
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

public sealed class CEShieldBeamOverlayDispatcher : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly IEntityManager _entities;
    private readonly Dictionary<NetEntity, CEShieldBeamOverlay> _instances = new();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    public readonly CEShieldBeamLowerOverlay LowerOverlay;
    private IClydeViewport? _preparedViewport;
    private int _preparedSplitDepth;
    private float _preparedScale;

    public CEShieldBeamOverlayDispatcher(IEntityManager entities)
    {
        _entities = entities;
        LowerOverlay = new CEShieldBeamLowerOverlay(this);
        ZIndex = -1;
    }

    public void PreparePass(IClydeViewport viewport,
        Func<EntityUid, Vector2, (int Depth, Vector2 Position, Vector2 Up)?> project,
        Func<EntityUid, float> cloudCoverage, float pixelsPerMeter, int splitDepth)
    {
        Vector2 ToWorld(Vector2 point) => viewport.LocalToWorld(point).Position;
        Populate((map, position) =>
        {
            if (project(map, position) is not { } point)
                return null;
            var world = ToWorld(point.Position);
            return (point.Depth, world, ToWorld(point.Position + point.Up) - world);
        }, cloudCoverage);
        _preparedViewport = viewport;
        _preparedSplitDepth = splitDepth;
        _preparedScale = Vector2.Distance(ToWorld(Vector2.Zero), ToWorld(new Vector2(pixelsPerMeter, 0f)));
    }

    public void RenderPass(IRenderHandle render, IClydeViewport viewport,
        Func<EntityUid, Vector2, (int Depth, Vector2 Position, Vector2 Up)?> project,
        Func<EntityUid, float> cloudCoverage, float pixelsPerMeter)
    {
        Populate(project, cloudCoverage);
        var handle = render.DrawingHandleScreen;
        handle.RenderInRenderTarget(viewport.RenderTarget, () => Render(handle, pixelsPerMeter), null);
    }

    private void Populate(Func<EntityUid, Vector2, (int Depth, Vector2 Position, Vector2 Up)?> project,
        Func<EntityUid, float> cloudCoverage)
    {
        foreach (var instance in _instances.Values)
        {
            instance.Points.Clear();
            instance.Clouds.Clear();
        }
        var transform = _entities.System<SharedTransformSystem>();
        var query = _entities.EntityQueryEnumerator<CEShieldBeamComponent, TransformComponent>();
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
            instance.Points.Add((point.Depth, point.Position, point.Up));
            if (_entities.HasComponent<CEZCloudLayerComponent>(pointMap))
                instance.Clouds.Add((point.Depth, point.Position, cloudCoverage(pointMap)));
        }
        foreach (var (id, instance) in _instances.ToArray())
        {
            if (instance.Points.Count != 0)
                continue;
            instance.Dispose();
            _instances.Remove(id);
        }
    }

    private void Render(DrawingHandleBase handle, float scale,
        int? fromDepth = null, int? toDepth = null, bool tip = true)
    {
        handle.UseShader(null);
        var time = (float) _timing.CurTime.TotalSeconds;
        foreach (var instance in _instances.Values)
            instance.Render(handle, scale, time, fromDepth, toDepth, tip);
    }

    public void DrawLower(in OverlayDrawArgs args)
    {
        if (args.Viewport == _preparedViewport)
            Render(args.DrawingHandle, _preparedScale, toDepth: _preparedSplitDepth, tip: false);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport != _preparedViewport)
            return;
        Render(args.DrawingHandle, _preparedScale, fromDepth: _preparedSplitDepth);
        _preparedViewport = null;
    }

    protected override void DisposeBehavior()
    {
        foreach (var instance in _instances.Values)
            instance.Dispose();
        _instances.Clear();
        base.DisposeBehavior();
    }
}

public sealed class CEShieldBeamLowerOverlay : Overlay
{
    private readonly CEShieldBeamOverlayDispatcher _beam;
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public CEShieldBeamLowerOverlay(CEShieldBeamOverlayDispatcher beam)
    {
        _beam = beam;
        ZIndex = 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _beam.DrawLower(args);
    }
}
