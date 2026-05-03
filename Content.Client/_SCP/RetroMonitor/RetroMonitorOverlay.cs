using Content.Shared._Scp.RetroMonitor; // Mono
using Robust.Client.Graphics;
using Robust.Client.Player; // Mono
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Scp.RetroMonitor;

public sealed class RetroMonitorOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!; // Mono
    [Dependency] IEntityManager _entityManager = default!; // Mono

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true; // Запрашиваем ScreenTexture

    private readonly ShaderInstance _retroShader;

    public RetroMonitorOverlay()
    {
        IoCManager.InjectDependencies(this);
        _retroShader = _prototypeManager.Index<ShaderPrototype>("crt_vhs").InstanceUnique();
    }

    // Mono start
    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_playerManager.LocalEntity is not { Valid: true } player
            || !_entityManager.HasComponent<RetroMonitorViewComponent>(player))
        {
            return false;
        }

        return base.BeforeDraw(in args);
    }
    // Mono end

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        _retroShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        var handle = args.WorldHandle;
        var viewport = args.WorldBounds;

        handle.UseShader(_retroShader);
        handle.DrawRect(viewport, Color.White);
        handle.UseShader(null);
    }
}
