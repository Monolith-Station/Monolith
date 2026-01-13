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
using System.Net.Http.Headers;

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
        Fallback = 1,
        Biome = 2,
        Grid = 3,
        Combat = 4
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
    private void SwitchCombatMusic(bool currentCombatState)
    {
        SetMusic(_lastGrid, _lastBiome, currentCombatState);
    }

    private void SetMusic(EntityUid? newGrid, ProtoId<SpaceBiomePrototype>? newBiome, bool newCombatState)
    {
        Log.Info("SETMUSIC: - GRID: " + newGrid.ToString() + " BIOME: " + newBiome.ToString() + " COMBAT: " + newCombatState.ToString());
        // priority list:
        // 1. (not implemented yet :godo:) ship combat music
        // 2. combat music
        // 3. grid music
        // 4. biome music
        // therefore we check these top 2 bottom

        // logic:
        /*
        1a. combat state different than cached - on // case: we turned on combatmode, we should update our cache and play
            play combat music
            update cache
            return
        1b. combat state different than cached - off // case: we turned off combatmode, and we should play either grid or biome music
            null cache
            continue
        ---- check ---- //case: biome or grid changes while combatmode is on
        is combat music on?
            update cache
            return
        ---- check ----
        2. are grids different - yes
            is grid music available - yes // case - moving from space/nonmusic grid to music grid
                play grid music
                update cache
                return
            is grid music available - no
                are we playing biome music - yes // case - moving from space/nonmusic grid to space/nonmusic grid
                    update cache
                    return
                are we playing biome music - no // case - moving from music grid to non-music grid
                    null biome cache
                    continue
        ---- check ---- //case: biome changes while grid music is on, ex: flagship halcyon moving across biomes
        is grid music on?
            update cache
            return
        ---- check ----
        3. are biomes different - yes
            is new biome null - yes
                set musicproto to default/fallback
            is newbiome null - no
                determine musicproto based on biome
                    if musicproto could not be found, set it to default/fallback (case: biome is defined but ambient music proto does not exist for it)
            play biome music
            update cache
            return

            play fallback music
            update cache
            return

        we should not be able to reach this point without any of the cases being caught
        fuck this code man - .2 | 2026

        */

        #region combat music
        if (newCombatState != _lastCombatState) //we switch combat music on or off now
        {
            Log.Info("REACHED COMBAT MUSIC - newCombatState: " + newCombatState.ToString());
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
                        combatFactionSuffix = "default";
                        break;
                }

                // if we find a ambient music prototype for our faction, then pick that one!
                if (_proto.TryIndex<AmbientMusicPrototype>("combatmode" + combatFactionSuffix, out var factionCombatMusicPrototype))
                    _musicProto = factionCombatMusicPrototype;
                else //if we don't ,set it to the default
                    _musicProto = _proto.Index<AmbientMusicPrototype>("combatmodedefault");

                SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);

                string path = _random.Pick(soundcol.PickFiles).ToString();

                _currentlyPlaying = MusicType.Combat;
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
            Log.Info("MUSIC CHANGE REQUESTED WHILE COMBATMODE IS ACTIVE - CACHE AND RETURN");
            _lastGrid = newGrid;
            _lastBiome = newBiome;
            return;
        }

        #region grid music

        if (newGrid != _lastGrid || _currentlyPlaying != MusicType.Grid)
        {
            if (newGrid != null && TryComp<VesselMusicComponent>(newGrid, out var music)) //do we have grid music? also this gives false if null
            {
                Log.Info("REACHED GRID, GRID HAS MUSIC");
                Log.Info("GRID IS NOT THE SAME AS LAST GRID - CACHE AND PLAY GRID MUSIC");
                _lastGrid = newGrid;
                _lastBiome = newBiome;
                _musicProto = _proto.Index<AmbientMusicPrototype>(music.AmbientMusicPrototype);
                SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);
                string path = _random.Pick(soundcol.PickFiles).ToString();
                _currentlyPlaying = MusicType.Grid;
                PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _ambientMusicFadeInTime, false);
                return;
            }
            else
            {
                // pass onto next
            }
        }
        else
            return;

        // if (_currentlyPlaying >= MusicType.Grid) // edge case: grid with music like halcyon is moving across biomes, we log the change and return
        // {
        //     Log.Info("BIOME MUSIC REQUEST WHILE GRID MUSIC IS PLAYING - CACHE AND RETURN");
        //     _lastGrid = newGrid;
        //     _lastBiome = newBiome;
        //     return;
        // }

        #endregion
        #region biome music

        if (_lastBiome != newBiome || _currentlyPlaying != MusicType.Biome) //if newBiome is null, we go to fallback
        {
            if (newBiome == null)
            {
                _musicProto = _proto.Index<AmbientMusicPrototype>("default");
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
                    Log.Info("BIOME - MUSICPROTO IS NULL, WE FOUND NO BIOME MUSIC. PLAY FALLBACK");
                    _musicProto = _proto.Index<AmbientMusicPrototype>("default");
                }
            }

            SoundCollectionPrototype soundcol = _proto.Index<SoundCollectionPrototype>(_musicProto.ID);

            string path = _random.Pick(soundcol.PickFiles).ToString();

            _lastBiome = newBiome; // update cache
            _lastGrid = newGrid;
            _currentlyPlaying = MusicType.Biome;
            PlayMusicTrack(path, _musicProto.Sound.Params.Volume, _ambientMusicFadeInTime, false);
            return;
        }
        else
            return;

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
        Log.Debug("ROUNDEND, KILLING MUSIC");
        _ambientMusicStream = _audio.Stop(_ambientMusicStream);
    }
    public void DisableAmbientMusic()
    {
        if (_ambientMusicStream == null)
        {
            _sawmill.Debug("AMBIENT MUSIC STREAM WAS NULL? FROM DisableAmbientMusic()");
            return;
        }
        Log.Debug("DISABLEAMBIENTMUSIC RAN??");
        FadeOut(_ambientMusicStream);
        _ambientMusicStream = null;
    }

}
