using Content.Client.Shuttles;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Content.Shared.Shuttles.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._FarHorizons.StarSystem;

public sealed partial class PlanetTransitSystem : SharedPlanetTransitSystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private PlanetTransitOverlay _overlay = default!;
    private static readonly SoundSpecifier FailureSound =
        new SoundPathSpecifier("/Audio/_CE/Explosions/thrusterfail.ogg", AudioParams.Default.AddVolume(3));
    private const float ShakeAmplitude = 1.2f;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<ShuttleConsoleComponent>(ShuttleConsoleUiKey.Key, subs =>
        {
            subs.Event<PlanetDescentRequestMessage>(OnDescentRequest);
        });
        SubscribeLocalEvent<PlanetTransitFailureComponent, ComponentStartup>(OnFailureStartup);

        _overlay = new PlanetTransitOverlay(EntityManager, _player, _timing, _prototypes, this);
        _overlays.AddOverlay(_overlay);
    }

    private void OnFailureStartup(Entity<PlanetTransitFailureComponent> ent, ref ComponentStartup args)
    {
        if (_player.LocalEntity is not { } player || Transform(player).GridUid != ent.Owner)
            return;

        var gain = _cfg.GetCVar(CCVars.AmbienceVolume);
        if (gain <= 0f)
            return;

        _audio.PlayGlobal(FailureSound, Filter.Local(), false,
            AudioParams.Default.WithVolume(2f + SharedAudioSystem.GainToVolume(gain)));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } player ||
            Transform(player).GridUid is not { } grid ||
            !TryComp<PlanetTransitFailureComponent>(grid, out var failure) ||
            !TryComp<CameraRecoilComponent>(player, out var recoil))
        {
            return;
        }

        var progress = (float) ((_timing.CurTime - failure.Start) / DischargeStunTime);
        if (progress is < 0f or >= 1f)
            return;

        var taper = 1f - progress;
        taper *= taper;
        var amplitude = Math.Min(ShakeAmplitude * taper * _cfg.GetCVar(CCVars.ScreenShakeIntensity), 1f);
        recoil.CurrentKick = _random.NextAngle().ToVec() * amplitude;
        recoil.LastKickTime = 0f;
    }

    private void OnDescentRequest(Entity<ShuttleConsoleComponent> ent, ref PlanetDescentRequestMessage args)
    {
        BeginPredictedDescent(ent, args);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay(_overlay);
        base.Shutdown();
    }

    public bool TryGetTransit(EntityUid grid, out PlanetTransitComponent transit)
    {
        if (TryComp<PlanetTransitComponent>(grid, out var ownTransit))
        {
            transit = ownTransit;
            return true;
        }

        var query = EntityQueryEnumerator<PlanetTransitComponent>();
        while (query.MoveNext(out _, out var candidate))
        {
            if (!candidate.Grids.Contains(grid))
                continue;

            transit = candidate;
            return true;
        }

        transit = default!;
        return false;
    }

    public bool TryGetFailure(EntityUid grid, out PlanetTransitFailureComponent failure)
    {
        if (TryComp<PlanetTransitFailureComponent>(grid, out var component))
        {
            failure = component;
            return true;
        }

        failure = default!;
        return false;
    }
}
