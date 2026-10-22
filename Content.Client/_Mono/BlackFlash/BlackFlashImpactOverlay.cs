using System.Numerics;
using Content.Shared._Mono.BlackFlash;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Mono.BlackFlash;

/// <summary>
/// This needs to be generalized into an impact frame system for later VFX reasons. Trust me I have a good reason for this.
/// </summary>
public sealed partial class BlackFlashImpactOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;

    public BlackFlashImpactOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _proto.Index<ShaderPrototype>("BlackFlashImpact").InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var player = _player.LocalEntity;

        if (!_entity.HasComponent<BlackFlashImpactFramesComponent>(player))
            return false;

        return _entity.TryGetComponent(player, out EyeComponent? eye) && args.Viewport.Eye == eye.Eye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null ||
            !_entity.TryGetComponent(_player.LocalEntity, out BlackFlashImpactFramesComponent? frames))
            return;

        var elapsed = _timing.CurTime - frames.Start;

        if (elapsed < TimeSpan.Zero || elapsed >= frames.Total)
            return;

        var fill = elapsed < frames.RedSpan ? frames.RedFill : frames.WhiteFill;

        var handle = args.WorldHandle;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("fillColor", new Vector3(fill.R, fill.G, fill.B));
        _shader.SetParameter("lineColor", new Vector3(frames.LineColor.R, frames.LineColor.G, frames.LineColor.B));

        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
