using Content.Shared._CE.Camera;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._CE.Camera;

public sealed partial class RadialShakeSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RadialShakeEvent>(OnRadialShake);
    }

    private void OnRadialShake(RadialShakeEvent ev)
    {
        if (_player.LocalEntity is { } player)
            Shake(player, ev.Duration, ev.Amplitude);
    }

    public void Shake(EntityUid uid, float duration, float amplitude)
    {
        if (duration <= 0f || amplitude <= 0f)
            return;

        var shake = EnsureComp<RadialShakeComponent>(uid);
        var now = _timing.CurTime;
        if (now + TimeSpan.FromSeconds(duration) >= shake.Start + TimeSpan.FromSeconds(shake.Duration))
        {
            shake.Start = now;
            shake.Duration = duration;
        }

        shake.Amplitude = MathF.Max(shake.Amplitude, amplitude);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } player ||
            !TryComp<RadialShakeComponent>(player, out var shake))
            return;

        var elapsed = (float) (_timing.CurTime - shake.Start).TotalSeconds;
        if (elapsed < 0f || elapsed >= shake.Duration)
        {
            RemCompDeferred<RadialShakeComponent>(player);
            return;
        }

        if (!TryComp<CameraRecoilComponent>(player, out var recoil))
            return;

        var taper = 1f - elapsed / shake.Duration;
        taper *= taper;
        var amplitude = Math.Min(shake.Amplitude * taper * _cfg.GetCVar(CCVars.ScreenShakeIntensity), 1f);
        recoil.CurrentKick = _random.NextAngle().ToVec() * amplitude;
        recoil.LastKickTime = 0f;
    }
}
