using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._CE.Camera;

public sealed partial class CEScreenFlashOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private readonly IGameTiming _timing;

    private TimeSpan _start;
    private float _duration;
    private Color _color = Color.White;

    public CEScreenFlashOverlay(IGameTiming timing)
    {
        _timing = timing;
    }

    public void Merge(TimeSpan now, float duration, Color color)
    {
        if (now + TimeSpan.FromSeconds(duration) >= _start + TimeSpan.FromSeconds(_duration))
        {
            _start = now;
            _duration = duration;
        }

        _color = color.A >= _color.A || Finished(now) ? color : _color;
    }

    public bool Finished(TimeSpan now)
    {
        return (float) (now - _start).TotalSeconds >= _duration;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var elapsed = (float) (_timing.CurTime - _start).TotalSeconds;
        if (_duration <= 0f || elapsed < 0f || elapsed >= _duration)
            return;
        var t = 1f - elapsed / _duration;
        var alpha = _color.A * t * t;

        args.ScreenHandle.DrawRect(
            new UIBox2(args.ViewportBounds.TopLeft, args.ViewportBounds.BottomRight),
            _color.WithAlpha(alpha));
    }
}
