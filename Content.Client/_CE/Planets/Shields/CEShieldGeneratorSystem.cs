using Content.Client._CE.Camera;
using Content.Shared._CE.Planets.Shields;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._CE.Planets.Shields;

public sealed partial class CEShieldGeneratorSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IClientGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private RadialShakeSystem _shake = default!;
    [Dependency] private CEScreenFlashSystem _flash = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private readonly Dictionary<NetEntity, GameTick> _pendingActivations = new();

    private CEShieldBeamOverlayDispatcher? _overlay;
    private CEShieldStartupOverlay? _startup;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEShieldPredictedActivationEvent>(OnPredictedActivation);
        SubscribeNetworkEvent<CEShieldActivationEvent>(OnConfirmedActivation);
        SubscribeLocalEvent<CEShieldGeneratorComponent, AfterAutoHandleStateEvent>(OnGeneratorState);
        EntityManager.EntityDeleted += OnEntityDeleted;
        _overlay = new CEShieldBeamOverlayDispatcher(EntityManager);
        _overlays.AddOverlay(_overlay.LowerOverlay);
        _overlays.AddOverlay(_overlay);
        _startup = new(EntityManager, _timing, _prototypes);
        _overlays.AddOverlay(_startup);
    }

    private void OnPredictedActivation(CEShieldPredictedActivationEvent predicted)
    {
        var ev = predicted.Activation;
        if (_players.LocalEntity is not { } player ||
            (_pendingActivations.TryGetValue(ev.Projector, out var tick) && tick == ev.Tick))
            return;

        _pendingActivations[ev.Projector] = ev.Tick;
        PlayActivation(ev, player);
    }

    private void OnConfirmedActivation(CEShieldActivationEvent ev)
    {
        if (_pendingActivations.TryGetValue(ev.Projector, out var tick) && tick <= ev.Tick)
        {
            _pendingActivations.Remove(ev.Projector);
            if (tick == ev.Tick)
                return;
        }

        if (_players.LocalEntity is { } player)
            PlayActivation(ev, player);
    }

    private void OnGeneratorState(Entity<CEShieldGeneratorComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var projector = GetNetEntity(ent.Owner);
        if (ent.Comp.Stage != CEShieldGeneratorStage.Active &&
            _pendingActivations.TryGetValue(projector, out var tick) && tick == _timing.LastRealTick)
            _pendingActivations.Remove(projector);
    }

    private void OnEntityDeleted(Entity<MetaDataComponent> ent) =>
        _pendingActivations.Remove(ent.Comp.NetEntity);

    private void PlayActivation(CEShieldActivationEvent ev, EntityUid player)
    {
        if (TryGetEntity(ev.Projector, out var projector) && projector is { } source && Transform(source).MapUid != null)
            _audio.PlayPvs(ev.Sound, source, ev.AudioParams);
        else if (TryGetEntity(ev.Origin.NetEntity, out _))
            _audio.PlayPvs(ev.Sound, GetCoordinates(ev.Origin), ev.AudioParams);

        _flash.Flash(ev.FlashDuration, Color.White);
        _shake.Shake(player, ev.ShakeDuration, ev.ShakeAmplitude);
    }

    public override void Shutdown()
    {
        EntityManager.EntityDeleted -= OnEntityDeleted;
        _pendingActivations.Clear();

        if (_startup != null)
        {
            _overlays.RemoveOverlay(_startup);
            _startup.Dispose();
            _startup = null;
        }

        if (_overlay != null)
        {
            _overlays.RemoveOverlay(_overlay.LowerOverlay);
            _overlay.LowerOverlay.Dispose();
            _overlays.RemoveOverlay(_overlay);
            _overlay.Dispose();
            _overlay = null;
        }
        base.Shutdown();
    }
}
