using System.Numerics;
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
    }

    protected override void DisposeBehavior()
    {
        _stencil?.Dispose();
        _darkness?.Dispose();
        base.DisposeBehavior();
    }
}
