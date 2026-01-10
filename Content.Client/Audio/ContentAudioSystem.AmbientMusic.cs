using System.Linq;
using Content.Client.Gameplay;
using Content.Shared._Crescent.SpaceBiomes;
using Content.Shared.Audio;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Client._Crescent.SpaceBiomes;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Client.CombatMode;
using Content.Shared.CombatMode;
using System.Threading;
using Robust.Shared.Timing;
using Content.Shared.NPC.Components;
using Content.Shared._Mono.CCVar;

namespace Content.Client.Audio;

/// <summary>
/// This handles playing ambient music over time, and combat music per faction.
/// </summary>
public sealed partial class ContentAudioSystem
{
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IStateManager _state = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly CombatModeSystem _combatModeSystem = default!; //CLIENT ONE. WHY ARE THERE 3???
    [Dependency] private readonly IPrototypeManager _protMan = default!;
    [Dependency] private readonly SpaceBiomeSystem _spaceBiome = default!;

    //options menu ---
    private static float _volumeSliderAmbient;
    private static float _volumeSliderCombat;
    private static bool _combatMusicToggle;
    //options menu ---

    private const string NpcFactionPDV = "PirateNF"; //we should really fucking change these on monolith. wtf
    private const string NpcFactionTSFMC = "TSFMC";

    // This stores the music stream. It's used to start/stop the music on the fly.
    private EntityUid? _ambientMusicStream;

    // This stores the ambient music prototype to be played next.
    private AmbientMusicPrototype? _musicProto;

    // Need to keep track of the last biome we were in to re-play its music when we're out of combat mode
    private SpaceBiomePrototype? _lastBiome;

    // Time to wait in between replaying ambient music tracks. Should be at least 1-2 seconds to prevent possible overlapping.
    private float _timeUntilNextAmbientTrack = 1; //value doesnt matter cuz itll be overridden

    // List of available ambient music tracks to sift through.
    private List<AmbientMusicPrototype>? _musicTracks;

    // Time in seconds for ambient music tracks to fade in. Set to 0 to play immediately.
    private float _ambientMusicFadeInTime = 10f;

    // Time in seconds for combat music tracks to fade in. Set to 0 to play immediately.
    private float _combatMusicFadeInTime = 2f;

    // Time that combat mode needs to be on to start playing music. Set to 0 to play immediately.
    private float _combatMusicTimeToStart = 3; //TODO: MAKE ME A CVAR

    // Time that combat mode needs to be off to stop combat mode. Set to 0 to turn off as soon as combat mode is off.
    private float _combatMusicTimeToEnd = 30; //TODO: MAKE ME A CVAR

    // Combat mode state before checking to switch combat music off/on.
    // 1. We toggle combat mode. We fire SwitchCombatMusic in (timer) seconds.
    // 2. We save the state from step 1 in _lastCombatState
    // 3. When SwitchCombatMusic fires, we check if the current combat state is different than _lastCombatState. If it is, then we change music. If not, we keep it.
    bool _lastCombatState = false;

    // This is for checking if we play station or biome music after combat mode turns off.
    // There's probably a better way to do this but nobody will care until this code gets refactored again
    // .2 | 2025
    bool _validStationMusic = false;

    // This stores the last station music we were in, so that we can play it when combat mode turns off.
    private string _lastStationMusic = "";
    // really stupid - i need this to check if the volume changes when you change the options menu options.
    private bool _isCombatMusicPlaying = false;

    private float _replayAmbientMusicTimer = 0;
    private bool _replayAmbientMusicBool;
    private float _combatWindUpTimer = 0;
    private bool _combatWindUpBool = false;
    private float _combatWindDownTimer = 0;
    private bool _combatWindDownBool = false;

    //used for logging, don't touch this
    private ISawmill _sawmill = default!;

    public void UpdateAmbientMusic(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted) //otherwise this will tick like 5x faster on client. thanks prediction
            return;

        if (_replayAmbientMusicBool)
        {
            _replayAmbientMusicTimer += frameTime;
            if (_replayAmbientMusicTimer > _timeUntilNextAmbientTrack)
            {
                ReplayAmbientMusic();
                _replayAmbientMusicTimer = 0;
            }
        }
        if (_combatWindUpBool)
        {
            _combatWindUpTimer += frameTime;
            if (_combatWindUpTimer > _combatMusicTimeToStart)
            {
                SwitchCombatMusic();
                _combatWindUpBool = false;
                _combatWindUpTimer = 0;
            }
        }
        if (_combatWindDownBool)
        {
            _combatWindDownTimer += frameTime;
            if (_combatWindDownTimer > _combatMusicTimeToEnd)
            {
                SwitchCombatMusic();
                _combatWindDownBool = false;
                _combatWindDownTimer = 0;
            }
        }
    }

    private void InitializeAmbientMusic()
    {
        SubscribeLocalEvent<SpaceBiomeSwapMessage>(OnBiomeChange);
        SubscribeLocalEvent<NewVesselEnteredMessage>(OnVesselChange);
        SubscribeLocalEvent<SpaceEnteredMessage>(OnSpaceEntered);
        SubscribeLocalEvent<ToggleCombatActionEvent>(OnCombatModeToggle);

        Subs.CVar(_configManager, CCVars.AmbientMusicVolume, AmbienceCVarChanged, true);
        Subs.CVar(_configManager, MonoCVars.CombatMusicVolume, CombatCVarChanged, true);
        Subs.CVar(_configManager, MonoCVars.CombatMusicEnabled, CombatToggleChanged, true);
        _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("audio.ambience");

        // Setup tracks to pull from. Runs once.
        _musicTracks = GetTracks();


        //no longer needed because we track my the current audio track's time
        //Timer.Spawn(_timeUntilNextAmbientTrack, () => ReplayAmbientMusic());

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnProtoReload);
        _state.OnStateChanged += OnStateChange;
        // On round end summary OR lobby cut audio.
        SubscribeNetworkEvent<RoundEndMessageEvent>(OnRoundEndMessage);
    }

    /// <summary>
    /// This function runs on a looping timer. The timer fires immediately after any AMBIENT (not combat) track is played, to play the next track.
    /// </summary>
    private void ReplayAmbientMusic()
    {
        if (_musicProto == null) //if we don't find any, we play the default track.
        {
            _musicProto = _proto.Index<AmbientMusicPrototype>("default");
            _lastBiome = _proto.Index<SpaceBiomePrototype>("default");
        }

        SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);

        string path = _random.Pick(soundcol.PickFiles).ToString(); // THIS WILL PICK A RANDOM SOUND. WE MAY WANT TO SPECIFY ONE INSTEAD!!

        PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _ambientMusicFadeInTime, false);
    }

    private void OnBiomeChange(ref SpaceBiomeSwapMessage ev)
    {

        SpaceBiomePrototype biome = _protMan.Index<SpaceBiomePrototype>(ev.Id); //get the biome prototype
        _lastBiome = biome; //save biome in case we are in combat mode

        if (_combatModeSystem.IsInCombatMode()) //we don't want to change music if we are in combat mode right now
            return;

        // if we're on the countsman or something moving, we don't want to switch the music
        if (_validStationMusic)
            return;

        FadeOut(_ambientMusicStream);

        if (_musicTracks == null)
            return;

        _musicProto = null;

        foreach (var ambient in _musicTracks)
        {
            if (biome.ID == ambient.ID) //if we find the biome that's matching the ambient's ID, we play that track!
            {
                //_sawmill.Debug($"found biome match: {biome.ID} == {ambient.ID}");
                _musicProto = ambient;
                break;
            }
        }

        if (_musicProto == null) //if we don't find any, we play the default track.
        {
            _musicProto = _proto.Index<AmbientMusicPrototype>("default");
            _lastBiome = _proto.Index<SpaceBiomePrototype>("default");
        }

        SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);

        string path = _random.Pick(soundcol.PickFiles).ToString(); // THIS WILL PICK A RANDOM SOUND. WE MAY WANT TO SPECIFY ONE INSTEAD!!

        PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _ambientMusicFadeInTime, false);
    }


    /// <summary>
    ///
    /// </summary>
    /// <param name="ev"></param>
    private void OnVesselChange(ref NewVesselEnteredMessage ev)
    {
        //_sawmill.Debug($"went to ship {ev.Name}"); //SHOULD BE ONE WORD. Jackal, Countsman, PortBalreska...

        if (ev.AmbientMusicPrototype == "")
        {
            if (_validStationMusic) //if we are currently playing station music, we want to clear it and play biome music.
            {
                SpaceEnteredMessage spaceMsg = new SpaceEnteredMessage();
                OnSpaceEntered(ref spaceMsg);
            } //which we do by going "hey we moved to space". this is dirty but it works
            //_sawmill.Debug("NO MUSIC FOUND FOR SHIP!");
            _validStationMusic = false; //regardless of above, set it to false for combatmode purposes
            return;
        }
        else
        {
            _validStationMusic = true;
            _lastStationMusic = ev.AmbientMusicPrototype;
            //_sawmill.Debug("MUSIC FOUND FOR SHIP! " + ev.AmbientMusicPrototype);
        }

        if (_combatModeSystem.IsInCombatMode()) //we don't want to change music if we are in combat mode right now
            return;

        FadeOut(_ambientMusicStream);

        if (_musicTracks == null)
            return;

        _musicProto = null;

        foreach (var ambient in _musicTracks)
        {
            if (ev.AmbientMusicPrototype == ambient.ID) //if we find the station that's matching the ambient's ID, we play that track!
            {
                //_sawmill.Debug($"found station match: {biome.ID} == {ambient.ID}");
                _musicProto = ambient;
                break;
            }
        }

        if (_musicProto == null) //if we don't find any, we play the default track.
        {
            _musicProto = _proto.Index<AmbientMusicPrototype>("default");
        }

        SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);

        string path = _random.Pick(soundcol.PickFiles).ToString(); // THIS WILL PICK A RANDOM SOUND. WE MAY WANT TO SPECIFY ONE INSTEAD!!

        PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _ambientMusicFadeInTime, false);
    }

    private void OnSpaceEntered(ref SpaceEnteredMessage ev)
    {
        //_sawmill.Debug($"ENTERED SPACE, STOPPING MUSIC");

        _validStationMusic = false;

        if (_combatModeSystem.IsInCombatMode()) //we don't want to change music if we are in combat mode right now
            return;

        FadeOut(_ambientMusicStream);

        if (_lastBiome == null) //this should never happen still
        {
            if (_player.LocalSession != null) //THIS LITERALLY CANNOT BE NULL!! BUT IT COMPLAINS IF I DONT PUT THIS HERE!!!
            {
                if (_spaceBiome.currentSource != null)
                {
                    _lastBiome = _proto.Index<SpaceBiomePrototype>(_spaceBiome.currentSource.Id);
                }
            }
        }

        if (_lastBiome == null)
            return;

        _musicProto = _proto.Index<AmbientMusicPrototype>(_lastBiome.ID); //THIS CAN FUCK UP! BECAUSE THE ID MIGHT NOT HAVE MUSIC AND BE A FALLBACK!
        SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);

        string path = _random.Pick(soundcol.PickFiles).ToString(); // THIS WILL PICK A RANDOM SOUND. WE MAY WANT TO SPECIFY ONE INSTEAD!!

        PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _ambientMusicFadeInTime, false);
    }


    private void OnCombatModeToggle(ToggleCombatActionEvent ev)
    {
        if (_combatMusicToggle == false)
            return;
        if (!_timing.IsFirstTimePredicted == true) //needed, because combat mode is predicted, and triggers 7 times otherwise.
            return;

        bool currentCombatState = _combatModeSystem.IsInCombatMode();
        if (currentCombatState) //if combat mode is being turned ON
        {
            _combatWindUpBool = true;
            _combatWindUpTimer = 0;
        }
        else
        {
            _combatWindDownBool = true;
            _combatWindDownTimer = 0;
        }

    }
    private void SwitchCombatMusic()
    {
        string factionComponentString = "";
        if (TryComp<NpcFactionMemberComponent>(_player.LocalEntity, out NpcFactionMemberComponent? factionComp))
            factionComponentString = factionComp.Factions.FirstOrDefault("");
        bool currentCombatState = _combatModeSystem.IsInCombatMode();

        if (_lastCombatState == currentCombatState)
            return;

        _lastCombatState = currentCombatState;

        FadeOut(_ambientMusicStream);

        if (currentCombatState) //true = we toggled combat ON.
        {
            string combatFactionSuffix = ""; //this is added to "combatmode" to create "combatmodePDV", etc, to fetch combat tracks.
            switch (factionComponentString) //this will hardcode the valid factions but until someone cleans up the frontier tags this looks way nicer
            {
                case NpcFactionPDV:
                    combatFactionSuffix = "PDV";
                    break;
                case NpcFactionTSFMC:
                    combatFactionSuffix = "TSFMC";
                    break;
                default:
                    combatFactionSuffix = factionComponentString;
                    break;
            }

            //if we find a ambient music prototype for our faction, then pick that one!
            if (_proto.TryIndex<AmbientMusicPrototype>("combatmode" + combatFactionSuffix, out var factionCombatMusicPrototype))
            {
                _musicProto = factionCombatMusicPrototype;
                SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);

                string path = _random.Pick(soundcol.PickFiles).ToString(); // THIS WILL PICK A RANDOM SOUND. WE MAY WANT TO SPECIFY ONE INSTEAD!!

                PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _combatMusicFadeInTime, true);
            }
            else //if the faction combat music prototype does not exist, instead fall back to the default.
            {
                _musicProto = _proto.Index<AmbientMusicPrototype>("combatmodedefault");
                SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);

                string path = _random.Pick(soundcol.PickFiles).ToString(); // THIS WILL PICK A RANDOM SOUND. WE MAY WANT TO SPECIFY ONE INSTEAD!!

                PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _combatMusicFadeInTime, true);
            }
        }
        else                    //false = we toggled combat OFF
        {
            if (_lastBiome == null) //this should never happen still
            {
                if (_player.LocalSession != null) //THIS LITERALLY CANNOT BE NULL!! BUT IT COMPLAINS IF I DONT PUT THIS HERE!!!
                {
                    if (_spaceBiome.currentSource != null)
                    {
                        _lastBiome = _proto.Index<SpaceBiomePrototype>(_spaceBiome.currentSource.Id);
                    }
                }
            }

            if (_lastBiome == null)
                return;


            // when combat mode turns off, do we have valid station music to play? if yes, play it. if not, play the biome's music.
            if (_validStationMusic == true)
            {
                _musicProto = _proto.Index<AmbientMusicPrototype>(_lastStationMusic);
            }
            else
            {
                _musicProto = _proto.Index<AmbientMusicPrototype>(_lastBiome.ID); //THIS CAN FUCK UP! BECAUSE THE ID MIGHT NOT HAVE MUSIC AND BE A FALLBACK!
            }
            SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);

            string path = _random.Pick(soundcol.PickFiles).ToString(); // THIS WILL PICK A RANDOM SOUND. WE MAY WANT TO SPECIFY ONE INSTEAD!!

            PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _ambientMusicFadeInTime, false);

        }
    }

    /// <summary>
    /// This is a helper function that actually plays the music tracks.
    /// </summary>
    /// <param name="path"> Path to music to play.</param>
    /// <param name="volume"> Volume modifier (put 0 to keep original volume).</param>
    /// <param name="fadein"> Seconds for the music to fade in. Put 0 for no fadein. </param>
    private void PlayMusicTrack(string path, float volume, float fadein, bool combatMode)
    {
        _isCombatMusicPlaying = combatMode;
        //_sawmill.Debug($"NOW PLAYING: {path}" + " | COMBAT MODE: " + _isCombatMusicPlaying);

        if (combatMode)
        {
            volume += _volumeSliderCombat;
            _replayAmbientMusicBool = false;
        }
        else
        {
            volume += _volumeSliderAmbient;
            _replayAmbientMusicBool = true;
        }

        var strim = _audio.PlayGlobal(
            path,
            Filter.Local(),
            false,
            AudioParams.Default.WithVolume(volume))!;

        _ambientMusicStream = strim.Value.Entity; //this plays it immediately, but fadein function later makes it actually fade in.

        if (fadein != 0)
            FadeIn(_ambientMusicStream, strim.Value.Component, fadein);

        _timeUntilNextAmbientTrack = (float)_audio.GetAudioLength(path).TotalSeconds;
    }

    private List<AmbientMusicPrototype> GetTracks()
    {
        List<AmbientMusicPrototype> musictracks = new List<AmbientMusicPrototype>();

        bool fallback = true;
        foreach (var ambience in _proto.EnumeratePrototypes<AmbientMusicPrototype>())
        {
            //_sawmill.Debug($"logged ambient sound {ambience.ID}");
            musictracks.Add(ambience);
            fallback = false;
        }

        if (fallback) //if we somehow FOUND NO MUSIC TRACKS
        {
            _sawmill.Debug($"NO MUSIC FOUND, SOMETHING IS WRONG!");
        }

        return musictracks;
    }
    private void AmbienceCVarChanged(float obj)
    {
        _volumeSliderAmbient = SharedAudioSystem.GainToVolume(obj);

        //this changes the music volume live, while the music is playing. otherwise, the line above that changes the slider is the one that matters.

        if (_ambientMusicStream != null && _musicProto != null && !_isCombatMusicPlaying)
        {
            _audio.SetVolume(_ambientMusicStream, _musicProto.Sound.Params.Volume + _volumeSliderAmbient);
        }
    }

    private void CombatCVarChanged(float obj)
    {
        _volumeSliderCombat = SharedAudioSystem.GainToVolume(obj);

        //this changes the music volume live, while the music is playing. otherwise, the line above that changes the slider is the one that matters.

        if (_ambientMusicStream != null && _musicProto != null && _isCombatMusicPlaying)
        {
            _audio.SetVolume(_ambientMusicStream, _musicProto.Sound.Params.Volume + _volumeSliderCombat);
        }
    }

    private void CombatToggleChanged(bool obj)
    {
        _combatMusicToggle = obj;

        if (_combatMusicToggle)
            return;

        bool currentCombatState = _combatModeSystem.IsInCombatMode();

        if (_lastCombatState == currentCombatState)
            return;

        _lastCombatState = currentCombatState;

        FadeOut(_ambientMusicStream);

        if (_lastBiome == null) //this should never happen still
        {
            if (_player.LocalSession != null) //THIS LITERALLY CANNOT BE NULL!! BUT IT COMPLAINS IF I DONT PUT THIS HERE!!!
            {
                if (_spaceBiome.currentSource != null)
                {
                    _lastBiome = _proto.Index<SpaceBiomePrototype>(_spaceBiome.currentSource.Id);
                }
            }
        }

        if (_lastBiome == null)
            return;


        // when combat mode turns off, do we have valid station music to play? if yes, play it. if not, play the biome's music.
        if (_validStationMusic == true)
        {
            _musicProto = _proto.Index<AmbientMusicPrototype>(_lastStationMusic);
        }
        else
        {
            _musicProto = _proto.Index<AmbientMusicPrototype>(_lastBiome.ID); //THIS CAN FUCK UP! BECAUSE THE ID MIGHT NOT HAVE MUSIC AND BE A FALLBACK!
        }
        SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID); //THIS IS WHAT ERRORS!

        string path = _random.Pick(soundcol.PickFiles).ToString(); // THIS WILL PICK A RANDOM SOUND. WE MAY WANT TO SPECIFY ONE INSTEAD!!

        PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _ambientMusicFadeInTime, false);
    }

    private void ShutdownAmbientMusic()
    {
        _state.OnStateChanged -= OnStateChange;
        _ambientMusicStream = _audio.Stop(_ambientMusicStream);
    }

    private void OnProtoReload(PrototypesReloadedEventArgs obj)
    {
        if (obj.WasModified<AmbientMusicPrototype>())
            _musicTracks = GetTracks();
    }
    ///<summary>
    /// This function handles the change from lobby to gameplay, disabling music when you're not in gameplay state.
    ///</summary>
    private void OnStateChange(StateChangedEventArgs obj)
    {
        if (obj.NewState is not GameplayState)
            DisableAmbientMusic();
    }

    private void OnRoundEndMessage(RoundEndMessageEvent ev)
    {
        if (_ambientMusicStream == null)
        {
            //_sawmill.Debug("AMBIENT MUSIC STREAM WAS NULL? FROM OnRoundEndMessage()");
            return;
        }
        // If scoreboard shows then just stop the music
        _ambientMusicStream = _audio.Stop(_ambientMusicStream);
    }
    public void DisableAmbientMusic()
    {
        if (_ambientMusicStream == null)
        {
            //_sawmill.Debug("AMBIENT MUSIC STREAM WAS NULL? FROM DisableAmbientMusic()");
            return;
        }
        FadeOut(_ambientMusicStream);
        _ambientMusicStream = null;
    }
}
