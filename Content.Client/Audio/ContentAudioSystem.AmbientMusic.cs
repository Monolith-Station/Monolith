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
using Robust.Shared.Timing;
using Content.Shared.NPC.Components;
using Content.Shared._Mono.CCVar;
using Content.Shared._Crescent.Vessel;
using Content.Shared.Ghost;

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

    private enum MusicType : byte //used to deal with edgecases of when music should not be overridden
    {
        None = 0,
        Biome = 1,
        Grid = 2,
        Combat = 3
    }
    private MusicType? _currentlyPlaying = MusicType.None;

    // really stupid - i need this to check if the volume changes when you change the options menu options.
    private bool _isCombatMusicPlaying = false;

    private float _replayAmbientMusicTimer = 0;
    private bool _replayAmbientMusicBool;
    private float _combatWindUpTimer = 0;
    private bool _combatWindUpBool = false;
    private float _combatWindDownTimer = 0;
    private bool _combatWindDownBool = false;


    // ok so - the parent change for the station happens too early and something fucks up
    // so the music system plays the music but you can't hear it. this is for OnPlayerSpawn to set the replayambientmusictimer to replay it, which fixes it
    // its hacky, but itll do for now. inb4 this line is still here 2 years later
    private const float InitialStationMusicTimeToDrop = 5f;
    private float _initialStationMusicTimer = 0f;
    private bool _initialStationMusicBool = false;
    private ISawmill _sawmill = default!; //lobbymusic.cs has a sawmill call so i can't remove this????

    private ProtoId<SpaceBiomePrototype> _defaultBiomeProto = "BiomeDefault"; //which biome proto is the fallback for null?

    public void UpdateAmbientMusic(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted) //otherwise this will tick like 5x faster on client. thanks prediction
            return;

        if (_initialStationMusicBool)
        {
            _initialStationMusicTimer += frameTime;
            if (_replayAmbientMusicTimer > InitialStationMusicTimeToDrop)
            {
                ReplayAmbientMusic();
                _initialStationMusicBool = false;
                _initialStationMusicTimer = 0;
                _replayAmbientMusicTimer = 0;
            }
        }
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
        SubscribeLocalEvent<ToggleCombatActionEvent>(OnCombatModeToggle);

        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetach); //in case u die in combatmode

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerSpawn);

        Subs.CVar(_configManager, CCVars.AmbientMusicVolume, AmbienceCVarChanged, true);
        Subs.CVar(_configManager, MonoCVars.CombatMusicVolume, CombatCVarChanged, true);
        Subs.CVar(_configManager, MonoCVars.CombatMusicEnabled, CombatToggleChanged, true);
        Subs.CVar(_configManager, MonoCVars.CombatMusicWindUpTime, CombatWindUpChanged, true);
        Subs.CVar(_configManager, MonoCVars.CombatMusicWindDownTime, CombatWindDownChanged, true);

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
    /// Tracks player spawning in to replay ambient music 5 seconds in, making sure station music plays.
    /// Currently, there is a bug that I can't fix, where after spawning in, station music won't kick in despite the PlayMusic function firing.
    /// </summary>
    /// <param name="ev"></param>
    private void OnPlayerSpawn(LocalPlayerAttachedEvent ev)
    {
        _initialStationMusicBool = true;
        _initialStationMusicTimer = 0f;

    }


    /// <summary>
    /// This makes sure that combatmode turns OFF when u ghost, or when ur in aghost and return to ur character.
    /// </summary>
    /// <param name="ev"></param>
    private void OnPlayerDetach(LocalPlayerDetachedEvent ev)
    {
        SetMusic(_lastGrid, _lastBiome, false);
    }


    /// <summary>
    /// Helper function to replay ambient music after it's done.
    /// </summary>
    private void ReplayAmbientMusic()
    {
        if (_musicProto == null) //if we don't find any, we play the default track.
        {
            _musicProto = _proto.Index<AmbientMusicPrototype>(_defaultBiomeProto);
            _lastBiome = _proto.Index<SpaceBiomePrototype>(_defaultBiomeProto);
        }

        SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);

        string path = _random.Pick(soundcol.PickFiles).ToString(); //picks a random track. if someone really cared we could make it make sure it doesnt play the same track twice

        PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _ambientMusicFadeInTime, false);
    }

    private void OnBiomeChange(ref SpaceBiomeSwapMessage ev)
    {
        SetMusic(_lastGrid, ev.Id, _lastCombatState);
    }
    private void OnPlayerParentChange(ref PlayerParentChangedMessage ev)
    {
        SetMusic(ev.Grid, _lastBiome, _lastCombatState);
    }
    private void SwitchCombatMusic(bool currentCombatState)
    {
        SetMusic(_lastGrid, _lastBiome, currentCombatState);
    }
    private void OnCombatModeToggle(ToggleCombatActionEvent ev)
    {
        if (_combatMusicToggle == false) // if cvar is off, don't bother
            return;
        if (!_timing.IsFirstTimePredicted == true) //needed, because combat mode is predicted, and triggers 7 times otherwise.
            return;
        bool currentCombatState = _combatModeSystem.IsInCombatMode();
        if (currentCombatState) //if combat mode is being turned ON
        {
            _combatWindUpBool = true;
            _combatWindUpTimer = 0;
            _combatWindDownBool = false;
            _combatWindDownTimer = 0;
        }
        else //if combat mode is being turned OFF
        {
            _combatWindDownBool = true;
            _combatWindDownTimer = 0;
            _combatWindUpBool = false;
            _combatWindUpTimer = 0;
        }
    }

    /// <summary>
    /// Function that takes in data from requests to change music and determines what music to play / if it should play it.
    /// </summary>
    /// <param name="newGrid"></param>
    /// <param name="newBiome"></param>
    /// <param name="newCombatState"></param>
    private void SetMusic(EntityUid? newGrid, ProtoId<SpaceBiomePrototype>? newBiome, bool newCombatState)
    {
        //Log.Info("SETMUSIC: - GRID: " + newGrid.ToString() + " BIOME: " + newBiome.ToString() + " COMBAT: " + newCombatState.ToString());
        // priority list:
        // 1. (not implemented yet :godo:) ship combat music
        // 2. combat music
        // 3. grid music
        // 4. biome music
        // therefore we check these top 2 bottom

        #region combat music
        if (newCombatState != _lastCombatState) //we switch combat music on or off now
        {
            _lastCombatState = newCombatState; // cache combat state since its different than the last
            if (newCombatState) //true = we toggled combat ON.
            {
                // figure out the faction we should play combat music for
                string factionComponentString = "";
                if (TryComp<NpcFactionMemberComponent>(_player.LocalEntity, out NpcFactionMemberComponent? factionComp))
                    factionComponentString = factionComp.Factions.FirstOrDefault("");
                string combatFactionSuffix; //this is added to "combatmode" to create "combatmodePDV", etc, to fetch combat tracks.
                switch (factionComponentString) //this will hardcode the valid factions but until someone cleans up the frontier tags this looks way nicer
                {
                    case NpcFactionPDV:
                        combatFactionSuffix = "PDV";
                        break;
                    case NpcFactionTSFMC:
                        combatFactionSuffix = "TSFMC";
                        break;
                    default:
                        combatFactionSuffix = "Default";
                        break;
                }

                // if we find a ambient music prototype for our faction, then pick that one!
                if (_proto.TryIndex<AmbientMusicPrototype>("CombatMode" + combatFactionSuffix, out var factionCombatMusicPrototype))
                    _musicProto = factionCombatMusicPrototype;
                else //if we don't ,set it to the default
                    _musicProto = _proto.Index<AmbientMusicPrototype>("CombatModeDefault");

                _currentlyPlaying = MusicType.Combat;

                SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);
                string path = _random.Pick(soundcol.PickFiles).ToString();
                PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _combatMusicFadeInTime, true);
                return;
            }
            else
            {
                //false = we toggled combat OFF, therefore we should play music from our other data we have in this current request.
                // the easiest way to do this is to set lastgrid & lastbiome to null.
                _currentlyPlaying = MusicType.None;
                _lastBiome = null;
                _lastGrid = null;
            }
        }
        #endregion

        if (_currentlyPlaying >= MusicType.Combat) //if we are in combatmode, we still want to cache info, but we want to return here so that we dont stop playing combatmode music
        {
            _lastGrid = newGrid;
            _lastBiome = newBiome;
            return;
        }

        #region grid music

        if (newGrid != _lastGrid || _currentlyPlaying != MusicType.Grid)
        {
            if (newGrid != null && TryComp<VesselMusicComponent>(newGrid, out var music)) //do we have grid music? also this gives false if null
            {
                _lastGrid = newGrid;
                _lastBiome = newBiome;
                _currentlyPlaying = MusicType.Grid;

                _musicProto = _proto.Index<AmbientMusicPrototype>(music.AmbientMusicPrototype);
                SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);
                string path = _random.Pick(soundcol.PickFiles).ToString();
                PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _ambientMusicFadeInTime, false);
                return;
            }
            else
            {
                // pass onto next
            }
        }
        else
        {
            _lastGrid = newGrid;
            _lastBiome = newBiome;
            return;
        }

        #endregion
        #region biome music

        if (_lastBiome != newBiome || _currentlyPlaying != MusicType.Biome) //if newBiome is null, we go to fallback
        {
            if (newBiome == null)
            {
                _musicProto = _proto.Index<AmbientMusicPrototype>(_defaultBiomeProto);
            }
            else
            {
                if (_musicTracks == null) // if this is null we have way bigger issues
                    return;
                _musicProto = null;
                //else
                foreach (var ambient in _musicTracks)
                {
                    if (newBiome.Value.Id == ambient.ID) //if we find the biome that's matching an ambientMusic prototype's ID, we play that set.
                    {
                        _musicProto = ambient;
                        break;
                    }
                }
                if (_musicProto == null) //if we don't find any ambient music matching our current biome in _musicTracks, we play the fallback track.
                {
                    _musicProto = _proto.Index<AmbientMusicPrototype>(_defaultBiomeProto);
                }
            }

            _lastBiome = newBiome; // update cache
            _lastGrid = newGrid;
            _currentlyPlaying = MusicType.Biome;

            SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);
            string path = _random.Pick(soundcol.PickFiles).ToString();
            PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _ambientMusicFadeInTime, false);
            return;
        }
        else
        {
            _lastGrid = newGrid;
            _lastBiome = newBiome;
            return;
        }

        #endregion
    }


    /// <summary>
    /// Helper function that sets up parameters, audio stream, fadein, and music volume.
    /// </summary>
    /// <param name="path"> Path to music to play.</param>
    /// <param name="volume"> Volume modifier (put 0 to keep original volume).</param>
    /// <param name="fadein"> Seconds for the music to fade in. Put 0 for no fadein. </param>
    private void PlayMusicTrack(string path, float volume, float fadein, bool combatMode)
    {
        _isCombatMusicPlaying = combatMode;
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

    /// <summary>
    /// Helper function that fetches music tracks to set up the list to pull from on initialize.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    private List<AmbientMusicPrototype> GetTracks()
    {
        List<AmbientMusicPrototype> musictracks = new List<AmbientMusicPrototype>();

        bool fallback = true;
        foreach (var ambience in _proto.EnumeratePrototypes<AmbientMusicPrototype>())
        {
            musictracks.Add(ambience);
            fallback = false;
        }

        if (fallback) //if we somehow FOUND NO MUSIC TRACKS
        {
            throw new NullReferenceException("found no music tracks defined");
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

    /// <summary>
    /// Handles the combat mode music cvar being toggled on/off.
    /// </summary>
    /// <param name="obj"></param>
    private void CombatToggleChanged(bool obj)
    {
        _combatMusicToggle = obj;

        if (_combatMusicToggle) // if the player turned combat music back ON, then we don't really care anymore and the system works as usual
            return;
        if (_state.CurrentState is not GameplayState)
            return; //catches this throwing a null reference exception if u had this cvar toggled, in lobby
                    //cuz this next setmusic plays music. and well. we havent collected the music yet when we join in
                    //and we dont need to change music if we're not ingame anyway

        //otherwise we should kill combat music thats playing rn if they turned it off, otherwise it gets STUCK on.
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
            return;
        }
        FadeOut(_ambientMusicStream);
        _ambientMusicStream = null;
    }

}
