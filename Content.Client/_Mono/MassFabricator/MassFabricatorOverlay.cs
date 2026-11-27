using System.Numerics;
using System.Runtime.InteropServices;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Client._Mono.MassFabricator;

public sealed partial class MassFabricatorOverlay : Overlay
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private IRenderTexture? _stencil;
    private IRenderTexture? _darkness;
    private ShaderInstance? _routeShader;
    private readonly List<DrawVertexUV2D> _routeStrip = new();
    private readonly DrawVertexUV2D[] _arrowhead = new DrawVertexUV2D[4];
    private readonly HashSet<Vector2i> _tiles = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public MassFabricatorOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Size.X <= 0 || args.Viewport.Size.Y <= 0)
            return;

        if (_stencil?.Texture.Size != args.Viewport.Size)
        {
            _stencil?.Dispose();
            _darkness?.Dispose();
            _stencil = _clyde.CreateRenderTarget(args.Viewport.Size,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "mass-fabricator-stencil");
            _darkness = _clyde.CreateRenderTarget(args.Viewport.Size,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "mass-fabricator-darkness");
        }

        var handle = args.WorldHandle;
        var inverse = args.Viewport.GetWorldToLocalMatrix();
        var bounds = args.WorldBounds;
        var player = _players.LocalEntity;
        _entities.TryGetComponent(player, out TransformComponent? transform);
        var gridUid = transform?.GridUid ?? EntityUid.Invalid;
        _entities.TryGetComponent(gridUid, out MapGridComponent? grid);
        _entities.TryGetComponent(gridUid, out BroadphaseComponent? broadphase);
        _tiles.Clear();

        handle.RenderInRenderTarget(_stencil!, () =>
        {
            if (grid == null || broadphase == null || player == null)
                return;

            _entities.System<MassFabricatorVisionSystem>().GetView(player.Value, (gridUid, broadphase, grid), bounds, _tiles);
            var gridMatrix = _entities.System<SharedTransformSystem>().GetWorldMatrix(gridUid);
            handle.SetTransform(Matrix3x2.Multiply(gridMatrix, inverse));
            var lookup = _entities.System<EntityLookupSystem>();
            foreach (var tile in _tiles)
                handle.DrawRect(lookup.GetLocalBounds(tile, grid.TileSize), Color.White);
        }, Color.Transparent);

        handle.RenderInRenderTarget(_darkness!, () =>
        {
            handle.SetTransform(inverse);
            handle.UseShader(_prototypes.Index<ShaderPrototype>("CameraStatic").Instance());
            handle.DrawRect(bounds, Color.White);
            handle.UseShader(null);
        }, Color.Black);

        handle.UseShader(_prototypes.Index<ShaderPrototype>("StencilMask").Instance());
        handle.DrawTextureRect(_stencil!.Texture, args.WorldBounds);
        handle.UseShader(_prototypes.Index<ShaderPrototype>("StencilDraw").Instance());
        handle.DrawTextureRect(_darkness!.Texture, args.WorldBounds);
        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
        DrawWaveRoute(args);
    }

    private void DrawWaveRoute(in OverlayDrawArgs args)
    {
        if (_entities.System<MassFabricatorClientSystem>().WaveRoute is not { Count: >= 2 } route)
            return;

        var transform = _entities.System<SharedTransformSystem>();
        if (!route[0].IsValid(_entities) || transform.GetMapId(route[0]) != args.MapId)
            return;

        const float halfWidth = 0.20f;
        _routeStrip.Clear();
        var arrowMetrics = Vector2.Zero;
        var distance = 0f;
        var previous = transform.ToMapCoordinates(route[0]).Position;
        var incoming = Vector2.Zero;
        var previousLength = 0f;
        for (var i = 1; i < route.Count; i++)
        {
            var next = transform.ToMapCoordinates(route[i]).Position;
            var delta = next - previous;
            var length = delta.Length();
            if (length <= 0f)
                continue;

            var direction = delta / length;
            var normal = new Vector2(-direction.Y, direction.X);
            if (_routeStrip.Count == 0)
                AddSection(previous - direction * 0.11f, normal * halfWidth, -0.11f);
            if (incoming == Vector2.Zero || Vector2.Dot(incoming, direction) > 0.99f)
            {
                AddSection(previous, normal * halfWidth, distance);
            }
            else
            {
                var radius = MathF.Min(0.32f, MathF.Min(previousLength, length) / 3f);
                for (var step = 0; step <= 6; step++)
                {
                    var t = step / 6f;
                    var u = 1f - t;
                    var center = previous + (direction * t * t - incoming * u * u) * radius;
                    var tangent = Vector2.Normalize(incoming * u + direction * t);
                    AddSection(center, new Vector2(-tangent.Y, tangent.X) * halfWidth, distance + (2f * t - 1f) * radius);
                }
            }
            if (i == route.Count - 1)
            {
                var tipLength = MathF.Min(length * 0.5f, 0.28f);
                var padding = MathF.Min(0.06f, (length - tipLength) * 0.25f);
                var arrowBase = next - direction * tipLength;
                var baseDistance = distance + length - tipLength;
                var glowStart = arrowBase - direction * padding;
                AddSection(glowStart, normal * halfWidth, baseDistance - padding);
                var wing = normal * halfWidth;
                var glowEnd = next + direction * 0.06f;
                var glowStartUv = -1f - baseDistance + padding;
                var glowEndUv = -1f - distance - length - 0.06f;
                _arrowhead[0] = new DrawVertexUV2D(glowStart - wing, new Vector2(0f, glowStartUv));
                _arrowhead[1] = new DrawVertexUV2D(glowStart + wing, new Vector2(1f, glowStartUv));
                _arrowhead[2] = new DrawVertexUV2D(glowEnd - wing, new Vector2(0f, glowEndUv));
                _arrowhead[3] = new DrawVertexUV2D(glowEnd + wing, new Vector2(1f, glowEndUv));
                arrowMetrics = new Vector2(baseDistance, tipLength);
            }
            distance += length;
            previous = next;
            incoming = direction;
            previousLength = length;
        }

        if (_routeStrip.Count < 4)
            return;

        var shader = _routeShader ??= _prototypes.Index<ShaderPrototype>("MonoMassFabricatorRoute").InstanceUnique();
        shader.SetParameter("ARROW_METRICS", arrowMetrics);
        var handle = args.WorldHandle;
        handle.UseShader(shader);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, Texture.White, CollectionsMarshal.AsSpan(_routeStrip));
        if (arrowMetrics.Y > 0f)
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, Texture.White, _arrowhead);
        handle.UseShader(null);

        void AddSection(Vector2 center, Vector2 width, float along)
        {
            _routeStrip.Add(new DrawVertexUV2D(center - width, new Vector2(0f, along + 1f)));
            _routeStrip.Add(new DrawVertexUV2D(center + width, new Vector2(1f, along + 1f)));
        }
    }

    protected override void DisposeBehavior()
    {
        _stencil?.Dispose();
        _darkness?.Dispose();
        _routeShader?.Dispose();
        base.DisposeBehavior();
    }
}
