using Content.Client.Audio;
using Content.Shared._Mono.CCVar;
using Content.Shared._Mono.CombatMusic;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client._Mono.CombatMusic;

public sealed class ClientCombatMusicSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ContentAudioSystem _contentAudio = default!;

    private bool _enabled = true;
    private EntityUid? _stream;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CombatMusicStartEvent>(OnStart);
        SubscribeNetworkEvent<CombatMusicStopEvent>(OnStop);
        Subs.CVar(_cfg, MonoCVars.CombatMusicEnabled, OnCVarChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        StopPlayback();
    }

    private void OnCVarChanged(bool enabled)
    {
        _enabled = enabled;
        if (!_enabled)
            StopPlayback();
    }

    private void StopPlayback()
    {
        if (_stream != null)
        {
            _audio.Stop(_stream);
            _stream = null;
        }
    }

    private void OnStart(CombatMusicStartEvent ev)
    {
        if (!_enabled)
            return;

        StopPlayback();

        var spec = new SoundPathSpecifier(ev.SoundPath);
        var parms = AudioParams.Default.WithVolume(ev.VolumeDb).WithLoop(ev.Loop);
        var stream = _audio.PlayGlobal(spec, Filter.Local(), false, parms);
        _stream = stream?.Entity;
    }

    private void OnStop(CombatMusicStopEvent ev)
    {
        if (_stream != null && ev.FadeOutDuration > 0f && TryComp(_stream, out AudioComponent? component))
        {
            _contentAudio.FadeOut(_stream, component, ev.FadeOutDuration);
            _stream = null;
        }
        else
        {
            StopPlayback();
        }
    }
}


