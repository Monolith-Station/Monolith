/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Client.Viewport;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._CE.ZLevels.Core.Overlays;

public sealed partial class CEZLevelBlurOverlay : Overlay
{
    private const int MaxBlurPasses = 12;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IClyde _clyde = default!;
    private readonly ShaderInstance _horizontal;
    private readonly ShaderInstance _vertical;
    private readonly float[] _weights = new float[MaxBlurPasses + 1];
    private IRenderTexture? _blurBuffer;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private readonly ProtoId<ShaderPrototype> _zBlurShader = "CEZBlur";

    public CEZLevelBlurOverlay()
    {
        // The ship shell draws after the deck blur and shades its own continuous depth.
        ZIndex = 0;
        IoCManager.InjectDependencies(this);
        var shader = _proto.Index(_zBlurShader);
        _horizontal = shader.InstanceUnique();
        _vertical = shader.InstanceUnique();
        _horizontal.SetParameter("AXIS", Vector2.UnitX);
        _horizontal.SetParameter("TINT", Vector3.One);
        _vertical.SetParameter("AXIS", Vector2.UnitY);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye is not ScalingViewport.ZEye zeye)
            return false;

        if (zeye.Depth >= 0)
            return false;

        if (args.MapId == MapId.Nullspace)
            return false;

        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture is not { } source || args.Viewport.Eye is not ScalingViewport.ZEye eye)
            return;

        var size = args.Viewport.Size;
        if (_blurBuffer?.Size != size)
        {
            _blurBuffer?.Dispose();
            _blurBuffer = _clyde.CreateRenderTarget(size, RenderTargetColorFormat.Rgba8Srgb,
                new TextureSampleParameters { WrapMode = TextureWrapMode.MirroredRepeat }, "Z-level blur");
        }

        var depth = MathF.Min(-eye.Depth, MaxBlurPasses);
        var radius = BuildKernel(depth);
        var screen = args.RenderHandle.DrawingHandleScreen;
        _horizontal.SetParameter("SCREEN_TEXTURE", source);
        _horizontal.SetParameter("KERNEL_RADIUS", radius);
        _horizontal.SetParameter("WEIGHTS", _weights);
        screen.RenderInRenderTarget(_blurBuffer, () =>
        {
            screen.UseShader(_horizontal);
            screen.DrawRect(new UIBox2(Vector2.Zero, size), Color.White);
            screen.UseShader(null);
        }, null);

        _vertical.SetParameter("SCREEN_TEXTURE", _blurBuffer.Texture);
        _vertical.SetParameter("KERNEL_RADIUS", radius);
        _vertical.SetParameter("WEIGHTS", _weights);
        var ambient = _entity.TryGetComponent<MapLightComponent>(args.MapUid, out var light)
            ? light.AmbientLightColor
            : Color.Blue;
        _vertical.SetParameter("TINT", DepthTint(new Vector3(ambient.R, ambient.G, ambient.B), depth));

        var worldHandle = args.WorldHandle;
        worldHandle.UseShader(_vertical);
        worldHandle.DrawRect(args.WorldBounds, Color.White);
        worldHandle.UseShader(null);
    }

    internal static Vector3 DepthTint(Vector3 ambient, float depth)
    {
        var fullPasses = MathF.Floor(depth);
        var tint = Vector3.Lerp(Vector3.One, ambient, 0.3f);
        return new Vector3(MathF.Pow(tint.X, fullPasses), MathF.Pow(tint.Y, fullPasses), MathF.Pow(tint.Z, fullPasses))
            * Vector3.Lerp(Vector3.One, tint, depth - fullPasses);
    }

    private int BuildKernel(float depth)
    {
        var radius = (int) MathF.Ceiling(depth);
        Array.Clear(_weights);
        _weights[0] = 1f;

        // Convolve CE's three taps once per deck, with a partial step during interpolation.
        for (var pass = 1; pass <= radius; pass++)
        {
            var side = MathF.Min(1f, depth - pass + 1f) / 3f;
            var center = 1f - 2f * side;
            var right = 0f;
            for (var tap = pass; tap > 0; tap--)
            {
                var previous = _weights[tap];
                _weights[tap] = center * previous + side * (_weights[tap - 1] + right);
                right = previous;
            }

            _weights[0] = center * _weights[0] + 2f * side * right;
        }

        return radius;
    }

    protected override void DisposeBehavior()
    {
        _blurBuffer?.Dispose();
        _horizontal.Dispose();
        _vertical.Dispose();
        base.DisposeBehavior();
    }
}
