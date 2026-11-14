using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared._Mono.DualWield;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Direction = Robust.Shared.Maths.Direction;

namespace Content.Client._Mono.DualWield;

/// <summary>
/// Draws both dual-wielded weapons flanking the cursor. The first-indexed hand's weapon goes on the left, the second's on the right.
/// </summary>
/// <remarks>
/// Structured after ShowHandItemOverlay, which this replaces while the stance is active.
/// Each side needs its own render target - they are blitted after the whole overlay is drawn, so sharing one would leave both sides showing the same sprite.
/// </remarks>
public sealed partial class DualWieldCursorOverlay : Overlay
{
    /// <summary>
    /// Horizontal distance in pixels from the cursor to the centre of each weapon sprite.
    /// </summary>
    private const float CursorGap = 32f;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IInputManager _inputManager = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IPlayerManager _player = default!;

    private SharedDualWieldSystem? _dualWield;
    private readonly IRenderTexture _leftBuffer;
    private readonly IRenderTexture _rightBuffer;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public DualWieldCursorOverlay()
    {
        IoCManager.InjectDependencies(this);

        _leftBuffer = CreateBuffer("left");
        _rightBuffer = CreateBuffer("right");
    }

    private IRenderTexture CreateBuffer(string side)
    {
        return _clyde.CreateRenderTarget(
            (64, 64),
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb, true),
            new TextureSampleParameters
            {
                Filter = true
            }, $"{nameof(DualWieldCursorOverlay)}-{side}");
    }

    protected override void DisposeBehavior()
    {
        base.DisposeBehavior();

        _leftBuffer.Dispose();
        _rightBuffer.Dispose();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_cfg.GetCVar(CCVars.HudHeldItemShow))
            return false;

        return base.BeforeDraw(in args);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } player
            || !_entMan.TryGetComponent(player, out DualWieldComponent? comp))
        {
            return;
        }

        var mousePos = _inputManager.MouseScreenPosition;

        // Offscreen
        if (mousePos.Window == WindowId.Invalid)
            return;

        _dualWield ??= _entMan.System<SharedDualWieldSystem>();

        var offset = _cfg.GetCVar(CCVars.HudHeldItemOffset);
        var offsetVec = new Vector2(offset, offset);
        var uiScale = (args.ViewportControl as Control)?.UIScale ?? 1f;

        DrawSide(args, (player, comp), true, mousePos.Position + offsetVec, uiScale);
        DrawSide(args, (player, comp), false, mousePos.Position + offsetVec, uiScale);
    }

    private void DrawSide(in OverlayDrawArgs args,
        Entity<DualWieldComponent> player,
        bool left,
        Vector2 cursorPos,
        float uiScale)
    {
        if (!_dualWield!.TryGetDualWeapon((player.Owner, player.Comp), left, out var weapon))
            return;

        if (!_entMan.TryGetComponent(weapon, out SpriteComponent? sprite))
            return;

        var buffer = left ? _leftBuffer : _rightBuffer;
        var halfSize = buffer.Size / 2;
        var screen = args.ScreenHandle;

        screen.RenderInRenderTarget(buffer, () =>
        {
            screen.DrawEntity(weapon, halfSize, new Vector2(1f, 1f) * uiScale, Angle.Zero, Angle.Zero, Direction.South, sprite);
        }, Color.Transparent);

        var gap = new Vector2(left ? -CursorGap : CursorGap, 0f) * uiScale;

        screen.DrawTexture(buffer.Texture, cursorPos - halfSize + gap, Color.White.WithAlpha(0.75f));
    }
}
