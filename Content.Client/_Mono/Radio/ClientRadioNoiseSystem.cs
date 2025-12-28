using Content.Shared._Mono.CCVar;
using Content.Shared._Mono.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;

namespace Content.Client._Mono.Radio;

/// <summary>
/// Client-side system that handles radio noise sounds.
/// </summary>
public sealed class ClientRadioNoiseSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RadioNoiseEvent>(OnRadioNoiseEvent);
    }

    private void OnRadioNoiseEvent(RadioNoiseEvent ev)
    {
        // Only play radio noise if the player has the option enabled
        if (!_cfg.GetCVar(MonoCVars.RadioNoiseEnabled))
            return;

        // Get the entity from the network event
        if (!TryGetEntity(ev.Entity, out var entity))
            return;

        // Play radio static sounds with custom volume
        var audioParams = AudioParams.Default.WithMaxDistance(1).WithVolume(-4f);

        switch (ev.ChannelId)
        {
            case "Common": // Broadband
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Crescent/Radio/radio_broadband.ogg"), entity.Value, audioParams);
                break;
            case "Traffic": // Shortband
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Crescent/Radio/radio_shortband.ogg"), entity.Value, audioParams);
                break;
            default: // Special
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Crescent/Radio/radio_other.ogg"), entity.Value, audioParams);
                break;
        }
    }
}
