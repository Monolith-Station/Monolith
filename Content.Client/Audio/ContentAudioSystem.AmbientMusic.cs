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
using Content.Shared._Crescent.Vessel;

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

    // Time to wait in between replaying ambient music tracks. Should be at least 1-2 seconds to prevent possible overlapping.
    private float _timeUntilNextAmbientTrack = 1;

    // List of available ambient music tracks to sift through.
    private List<AmbientMusicPrototype>? _musicTracks;

    // Time in seconds for ambient music tracks to fade in. Set to 0 to play immediately.
    private float _ambientMusicFadeInTime = 10f;

    // Time in seconds for combat music tracks to fade in. Set to 0 to play immediately.
    private float _combatMusicFadeInTime = 2f;

    // Time that combat mode needs to be on to start playing music. Set to 0 to play immediately.
    private float _combatMusicTimeToStart;

    // Time that combat mode needs to be off to stop combat mode. Set to 0 to turn off as soon as combat mode is off.
    private float _combatMusicTimeToEnd;

    // Combat mode state before checking to switch combat music off/on.
    // 1. We toggle combat mode. We fire SwitchCombatMusic in (timer) seconds.
    // 2. We save the state from step 1 in _lastCombatState
    // 3. When SwitchCombatMusic fires, we check if the current combat state is different than _lastCombatState. If it is, then we change music. If not, we keep it.
    private bool _lastCombatState = false;

    private ProtoId<SpaceBiomePrototype>? _lastBiome;
    private EntityUid? _lastGrid;


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
                SwitchCombatMusic(true);
                _combatWindUpBool = false;
                _combatWindUpTimer = 0;
            }
        }
        if (_combatWindDownBool)
        {
            _combatWindDownTimer += frameTime;
            if (_combatWindDownTimer > _combatMusicTimeToEnd)
            {
                SwitchCombatMusic(false);
                _combatWindDownBool = false;
                _combatWindDownTimer = 0;
            }
        }
    }

    private void InitializeAmbientMusic()
    {
        SubscribeLocalEvent<SpaceBiomeSwapMessage>(OnBiomeChange);
        SubscribeLocalEvent<PlayerParentChangedMessage>(OnPlayerParentChange);
        //SubscribeLocalEvent<SpaceEnteredMessage>(OnSpaceEntered);
        SubscribeLocalEvent<ToggleCombatActionEvent>(OnCombatModeToggle);

        Subs.CVar(_configManager, CCVars.AmbientMusicVolume, AmbienceCVarChanged, true);
        Subs.CVar(_configManager, MonoCVars.CombatMusicVolume, CombatCVarChanged, true);
        Subs.CVar(_configManager, MonoCVars.CombatMusicEnabled, CombatToggleChanged, true);
        Subs.CVar(_configManager, MonoCVars.CombatMusicWindUpTime, CombatWindUpChanged, true);
        Subs.CVar(_configManager, MonoCVars.CombatMusicWindDownTime, CombatWindDownChanged, true);
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

    private void ReplayAmbientMusic()
    {
        if (_musicProto == null) //if we don't find any, we play the default track.
        {
            _musicProto = _proto.Index<AmbientMusicPrototype>("default");
            _lastBiome = _proto.Index<SpaceBiomePrototype>("default");
        }

        SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);

        string path = _random.Pick(soundcol.PickFiles).ToString(); //picks a random track. if someone really cared we could make it make sure it doesnt play the same track twice

        PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _ambientMusicFadeInTime, false);
    }

    private void OnBiomeChange(ref SpaceBiomeSwapMessage ev)
    {
        SetMusic(_lastGrid, ev.Id, _lastCombatState);
    }


    /// <summary>
    ///
    /// </summary>
    /// <param name="ev"></param>
    private void OnPlayerParentChange(ref PlayerParentChangedMessage ev)
    {
        SetMusic(ev.Grid, _lastBiome, _lastCombatState);
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
    private void SwitchCombatMusic(bool currentCombatState)
    {
        SetMusic(_lastGrid, _lastBiome, currentCombatState);
    }

    private void SetMusic(EntityUid? newGrid, ProtoId<SpaceBiomePrototype>? newBiome, bool newCombatState)
    {
        bool passPriorityToNext = false; // if we need to cache data but let the next priority level down still run, set this to true.
        // priority list:
        // 1. (not implemented yet :godo:) ship combat music
        // 2. combat music
        // 3. grid music
        // 4. biome music
        // therefore we check these top 2 bottom

        #region combat music
        if (newCombatState != _lastCombatState) //we switch combat music on or off now
        {
            _lastCombatState = newCombatState;

            // 2.2 figure out if we turn combat music ON or OFF
            if (newCombatState) //true = we toggled combat ON.
            {
                // 2.3a figure out the faction we should play combat music for
                string factionComponentString = "";
                if (TryComp<NpcFactionMemberComponent>(_player.LocalEntity, out NpcFactionMemberComponent? factionComp))
                    factionComponentString = factionComp.Factions.FirstOrDefault("");
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

                // 2.4aif we find a ambient music prototype for our faction, then pick that one!
                if (_proto.TryIndex<AmbientMusicPrototype>("combatmode" + combatFactionSuffix, out var factionCombatMusicPrototype))
                {
                    _musicProto = factionCombatMusicPrototype;
                    SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);

                    string path = _random.Pick(soundcol.PickFiles).ToString();

                    PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _combatMusicFadeInTime, true);
                    return;
                }
                else // 2.5a if the faction combat music prototype does not exist, instead fall back to the default.
                {
                    _musicProto = _proto.Index<AmbientMusicPrototype>("combatmodedefault");
                    SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);

                    string path = _random.Pick(soundcol.PickFiles).ToString();

                    PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _combatMusicFadeInTime, true);
                    return;
                }
            }
            //false = we toggled combat OFF, therefore we should play music from our other data we have. easiest way is to just let them run with the pass bool.
            else //2.1b
                passPriorityToNext = true;
        }
        #endregion

        if (newCombatState) // don't wanna change music if we are in combat mode
            return;

        #region grid music
        if (newGrid != _lastGrid || passPriorityToNext == true)
        {
            passPriorityToNext = false;
            // issue: if you go into space and walk back onto a grid that has no biome music, it'll replay the biome music. this sucks, so,
            if (newGrid == null) //if we just moved onto space, we should play biome music. pass priority to next.
            {
                passPriorityToNext = true;
            }
            else //if we just moved onto a grid, get the vesselmusic comp and play that music.
            {
                if (TryComp<VesselMusicComponent>(newGrid, out var music)) //case 1: vessel did have music
                {
                    _musicProto = _proto.Index<AmbientMusicPrototype>(music.AmbientMusicPrototype);
                    SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);
                    string path = _random.Pick(soundcol.PickFiles).ToString();
                    PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _ambientMusicFadeInTime, false);
                    return;
                }
                else //case 2: vessel did not have music.
                {
                    if (_lastGrid == null) //if our last grid was null (empty space), it'd replay biome music if we let it continue, so...
                    {
                        _lastGrid = newGrid;
                        return; //...just don't do anything music wise!
                    }
                    else
                        passPriorityToNext = true; // otherwise, if we're moving from musical grid to boring grid, let the biome music take over.
                }
            }
        }
        #endregion
        #region biome music
        if (newBiome != _lastBiome || passPriorityToNext)
        {
            _lastBiome = newBiome;

            if (_musicTracks == null) // if this is null we have way bigger issues
                return;

            _musicProto = null;

            if (newBiome == null) // if we have no biome in range anymore, we should play the fallback track
            {
                _musicProto = _proto.Index<AmbientMusicPrototype>("default");
            }
            else // if we do have a biome, which should always be the case
            {
                foreach (var ambient in _musicTracks)
                {
                    if (newBiome.Value.Id == ambient.ID) //if we find the biome that's matching an ambientMusic prototype's ID, we play that set.
                    {
                        _musicProto = ambient;
                        break;
                    }
                }
            }
            if (_musicProto == null) //if we don't find any biome matching an ambient music set in _musicTracks, we play the default track.
            {
                _musicProto = _proto.Index<AmbientMusicPrototype>("default");
            }

            SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);

            string path = _random.Pick(soundcol.PickFiles).ToString(); // THIS WILL PICK A RANDOM SOUND. WE MAY WANT TO SPECIFY ONE INSTEAD!!

            PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _ambientMusicFadeInTime, false);
        }
        #endregion

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
        _sawmill.Debug($"NOW PLAYING: {path}" + " | COMBAT MODE: " + _isCombatMusicPlaying);
        FadeOut(_ambientMusicStream);

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
    private void CombatWindUpChanged(int obj)
    {
        _combatMusicTimeToStart = obj;
    }
    private void CombatWindDownChanged(int obj)
    {
        _combatMusicTimeToEnd = obj;
    }

    private void CombatToggleChanged(bool obj)
    {
        _combatMusicToggle = obj;

        if (_combatMusicToggle) // if the player turned combat music back ON, then we don't really care anymore and the system works as usual
            return;

        //otherwise we should kill combat music thats playing rn if they turned it off, otherwise it gets STUCK on.
        // TODO: MAKE SURE THIS ACTUALLY WORKS
        SetMusic(_lastGrid, _lastBiome, false);
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
