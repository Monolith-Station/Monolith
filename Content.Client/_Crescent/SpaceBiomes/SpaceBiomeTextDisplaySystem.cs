using Content.Shared._Crescent.SpaceBiomes;
using Robust.Shared.Prototypes;
using Content.Client.Audio;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._Crescent.SpaceBiomes;

public sealed class SpaceTextDisplaySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protMan = default!;
    [Dependency] private readonly IOverlayManager _overMan = default!;
    [Dependency] private readonly ContentAudioSystem _audioSys = default!;

    private SpaceBiomeTextOverlay _overlay = default!;

    //TODO: undo timer setup, do float update accumulator instead
    // private TimeSpan _cooldown = TimeSpan.FromMinutes(2); //used to prevent spamming
    // private bool _canDisplayText = true; //used to prevent spamming

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpaceBiomeSwapMessage>(OnSwap);
        SubscribeLocalEvent<NewVesselEnteredMessage>(OnNewVesselEntered);
        _overlay = new();
        _overMan.AddOverlay(_overlay);
    }

    private void OnSwap(ref SpaceBiomeSwapMessage ev)
    {
        _audioSys.DisableAmbientMusic();
        SpaceBiomePrototype biome = _protMan.Index<SpaceBiomePrototype>(ev.Biome);
        _overlay.Reset();
        _overlay.ResetDescription();
        _overlay.Text = biome.Name;
        _overlay.TextDescription = biome.Description;
        _overlay.CharInterval = TimeSpan.FromSeconds(2f / biome.Name.Length);
        if (_overlay.TextDescription == "")                   //if we have a biome with no description, it's default is "" and that has length 0.
            _overlay.CharIntervalDescription = TimeSpan.Zero;       //we need to calculate it here because otherwise...
        else
            _overlay.CharIntervalDescription = TimeSpan.FromSeconds(2f / biome.Description.Length);      //this would throw an exception
    }

    private void OnNewVesselEntered(ref NewVesselEnteredMessage ev)
    {
        // if (!_canDisplayText) //if we displayed during the last 2 min, don't do that
        //     return;
        // else
        // {
        //     _canDisplayText = false; //else, prevent displaying the next, and set up to clear this flag in _cooldown, which at the time of writing is 2 min
        //     Timer.Spawn(_cooldown, () => { _canDisplayText = true; });
        // }
        _overlay.Reset();             //these should be reset as well to match OnSwap
        _overlay.ResetDescription();

        if (_overlay.Text != null) //i dont know why this is here but im not touching it
            return;

        _overlay.Text = ev.Name;
        _overlay.TextDescription = ev.Description; // fallback is "" if no description is found.
        _overlay.CharInterval = TimeSpan.FromSeconds(2f / _overlay.Text.Length);

        if (_overlay.TextDescription == "")
            _overlay.CharIntervalDescription = TimeSpan.Zero; //if this is not done it tries dividing by 0 in the "else" clause
        else
            _overlay.CharIntervalDescription = TimeSpan.FromSeconds(2f / _overlay.TextDescription.Length);
    }
}
